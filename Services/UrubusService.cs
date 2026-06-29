using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using BusEnVivo.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BusEnVivo.Services;

/// <summary>
/// Cliente de los horarios interurbanos de urubus.com.uy.
///
///   GET https://www.urubus.com.uy/es/omnibus-horarios?ter_from={id}&ter_to={id}&go_date=DD/MM/YYYY&seats=N
///
/// Devuelve la página completa (server-side); cada servicio es un
/// &lt;div class="booking-item-container" empresa=... salida=... llegada=... precio=... ...&gt;.
/// El catálogo de terminales sale de los arrays JS from_cities / to_cities de esa misma página.
/// </summary>
public class UrubusService : IUrubusService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly ILogger<UrubusService> _logger;

    private readonly string _baseUrl;
    private readonly TimeSpan _ttlTerminales;
    private readonly TimeSpan _ttlResultados;

    private const string CacheKeyTerminales = "urubus:terminales";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public UrubusService(HttpClient http, IMemoryCache cache, IConfiguration config, ILogger<UrubusService> logger)
    {
        _http = http;
        _cache = cache;
        _logger = logger;
        _baseUrl = (config["Urubus:BaseUrl"] ?? "https://www.urubus.com.uy").TrimEnd('/');
        _ttlTerminales = TimeSpan.FromHours(double.TryParse(config["Urubus:TerminalesHoras"], out var h) ? h : 12);
        _ttlResultados = TimeSpan.FromMinutes(double.TryParse(config["Urubus:ResultadosMinutos"], out var m) ? m : 8);
    }

    // ---- Catálogo de terminales ----

    public async Task<CatalogoTerminales> GetTerminalesAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKeyTerminales, out CatalogoTerminales? cached) && cached is not null)
            return cached;

        // El listado COMPLETO de terminales está en la home como availableTags = [{id,value}, ...].
        // (En /es/omnibus-horarios, to_cities solo trae destinos desde un origen, sin Montevideo.)
        var html = await GetHtmlAsync($"{_baseUrl}/", ct);
        var terminales = ExtraerCiudades(html, "availableTags");

        // Origen y destino comparten el mismo universo de terminales.
        var catalogo = new CatalogoTerminales { Origenes = terminales, Destinos = terminales };
        _cache.Set(CacheKeyTerminales, catalogo, _ttlTerminales);

        _logger.LogInformation("urubus: catálogo de {N} terminales", terminales.Count);
        return catalogo;
    }

    /// <summary>Extrae un array JS tipo {nombre}_cities = [{"id":"83","value":"MONTEVIDEO (Todas)"}, ...].</summary>
    private static IReadOnlyList<Terminal> ExtraerCiudades(string html, string varName)
    {
        var m = Regex.Match(html, varName + @"\s*=\s*(\[.*?\])\s*;", RegexOptions.Singleline);
        if (!m.Success) return Array.Empty<Terminal>();
        try
        {
            var raw = JsonSerializer.Deserialize<List<CiudadDto>>(m.Groups[1].Value, JsonOpts);
            return raw?
                .Where(c => !string.IsNullOrWhiteSpace(c.Id) && !string.IsNullOrWhiteSpace(c.Value))
                .Select(c => new Terminal { Id = c.Id!, Nombre = c.Value! })
                .ToList() ?? new List<Terminal>();
        }
        catch (JsonException)
        {
            return Array.Empty<Terminal>();
        }
    }

    private sealed class CiudadDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("value")] public string? Value { get; set; }
    }

    // ---- Búsqueda de servicios ----

    public async Task<BusquedaHorarios> BuscarAsync(
        string fromId, string toId, DateOnly fecha, int asientos = 1, CancellationToken ct = default)
    {
        var cacheKey = $"urubus:buscar:{fromId}:{toId}:{fecha:yyyyMMdd}:{asientos}";
        if (_cache.TryGetValue(cacheKey, out BusquedaHorarios? cached) && cached is not null)
            return cached;

        // Nombres de las terminales (cosméticos y para el resultado): best-effort desde el catálogo.
        string nombreFrom = "", nombreTo = "";
        try
        {
            var cat = await GetTerminalesAsync(ct);
            nombreFrom = cat.Origenes.FirstOrDefault(t => t.Id == fromId)?.Nombre ?? "";
            nombreTo = cat.Destinos.FirstOrDefault(t => t.Id == toId)?.Nombre ?? "";
        }
        catch { /* el catálogo es opcional para buscar: seguimos solo con los IDs */ }

        var url = BuildSearchUrl(fromId, toId, nombreFrom, nombreTo, fecha, asientos);
        var html = await GetHtmlAsync(url, ct);
        var servicios = ParsearServicios(html);

        var resultado = new BusquedaHorarios
        {
            Origen = nombreFrom,
            Destino = nombreTo,
            Fecha = fecha,
            Servicios = servicios,
            UrlFuente = url
        };
        _cache.Set(cacheKey, resultado, _ttlResultados);

        _logger.LogInformation("urubus: {Count} servicios {From}->{To} {Fecha}",
            servicios.Count, fromId, toId, fecha.ToString("yyyy-MM-dd"));
        return resultado;
    }

    private static IReadOnlyList<Servicio> ParsearServicios(string html)
    {
        var parser = new HtmlParser();
        var doc = parser.ParseDocument(html);

        var servicios = new List<Servicio>();
        foreach (var node in doc.QuerySelectorAll("div.booking-item-container"))
        {
            string Attr(string n) => node.GetAttribute(n)?.Trim() ?? "";

            var empresa = Attr("empresa");
            if (string.IsNullOrWhiteSpace(empresa)) continue;   // descarta plantillas/encabezados

            // Origen y destino son los dos h5.booking-item-destination (salida, llegada).
            var dest = node.QuerySelectorAll("h5.booking-item-destination");
            var h3 = node.QuerySelector("h3");
            var ruta = node.QuerySelector(".booking-item-arrival p.booking-item-flight-class");
            var btn = node.QuerySelector("a[onclick*='goNext']");

            servicios.Add(new Servicio
            {
                Empresa = empresa,
                NumeroServicio = h3 is null ? "" : SoloDigitos(h3.TextContent),
                Categoria = Attr("trayecto"),
                Salida = Hora(Attr("salida")),
                Llegada = Hora(Attr("llegada")),
                Origen = dest.Length > 0 ? dest[0].TextContent.Trim() : "",
                Destino = dest.Length > 1 ? dest[1].TextContent.Trim() : "",
                Duracion = Duracion(Attr("duracion")),
                Precio = Attr("precio"),
                Asientos = int.TryParse(SoloDigitos(Attr("asientos")), out var a) ? a : 0,
                Comodidad = Attr("comodidad"),
                Ruta = ruta is null ? "" : Regex.Replace(ruta.TextContent, @"\s+", " ").Trim(),
                FechaHoraSalida = btn is null ? "" : FechaHoraDesdeOnclick(btn.GetAttribute("onclick"))
            });
        }
        return servicios;
    }

    /// <summary>Arma la URL de búsqueda de /es/omnibus-horarios. Los nombres son cosméticos.</summary>
    private string BuildSearchUrl(string fromId, string toId, string fromName, string toName, DateOnly fecha, int asientos)
    {
        var fechaStr = fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        return $"{_baseUrl}/es/omnibus-horarios?iyv=1&s=0" +
               $"&ter_from_list={Uri.EscapeDataString(fromName)}&ter_from={Uri.EscapeDataString(fromId)}" +
               $"&ter_to_list={Uri.EscapeDataString(toName)}&ter_to={Uri.EscapeDataString(toId)}" +
               $"&go_date={Uri.EscapeDataString(fechaStr)}&go_date_submit={Uri.EscapeDataString(fechaStr)}" +
               $"&seats={asientos}&btn_search=Buscar";
    }

    // ---- HTTP ----

    /// <summary>
    /// GET que lee bytes y decodifica como UTF-8. No usamos GetStringAsync porque urubus
    /// declara "charset=UTF8" (sin guion), un valor que .NET rechaza al parsear el Content-Type.
    /// </summary>
    private async Task<string> GetHtmlAsync(string url, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        var bytes = await resp.Content.ReadAsByteArrayAsync(ct);
        return Encoding.UTF8.GetString(bytes);
    }

    // ---- Helpers de formato ----

    private static string SoloDigitos(string s) => new(s.Where(char.IsDigit).ToArray());

    /// <summary>"0100" -> "01:00".</summary>
    private static string Hora(string hhmm)
    {
        var d = SoloDigitos(hhmm).PadLeft(4, '0');
        return d.Length >= 4 ? $"{d[..2]}:{d.Substring(2, 2)}" : hhmm;
    }

    /// <summary>" 205" -> "2h 05m".</summary>
    private static string Duracion(string raw)
    {
        var d = SoloDigitos(raw);
        if (d.Length < 3) return raw.Trim();
        var min = d[^2..];
        var hrs = d[..^2];
        return int.TryParse(hrs, out var h) ? $"{h}h {min}m" : raw.Trim();
    }

    /// <summary>goNext(0, '2026-07-04 01:00', 83, 101, ...) -> "2026-07-04 01:00".</summary>
    private static string FechaHoraDesdeOnclick(string? onclick)
    {
        if (string.IsNullOrEmpty(onclick)) return "";
        var m = Regex.Match(onclick, @"goNext\(\s*[^,]*,\s*'([^']+)'");
        return m.Success ? m.Groups[1].Value.Trim() : "";
    }
}
