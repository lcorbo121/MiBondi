using System.Text;
using System.Text.Json;
using BusEnVivo.Models;

namespace BusEnVivo.Services;

/// <summary>
/// Cliente del endpoint en tiempo real de la STM (Intendencia de Montevideo).
///
///   POST https://www.montevideo.gub.uy/buses/rest/stm-online
///   Content-Type: application/json
///   Body  {}                       -> todos los ómnibus
///   Body  {"lineas":["155","103"]} -> sólo esas líneas
///
/// Responde un GeoJSON FeatureCollection con coordenadas [lng, lat] (EPSG:4326).
/// </summary>
public class StmService : IStmService
{
    private readonly HttpClient _http;
    private readonly ILogger<StmService> _logger;
    private readonly string _endpoint;
    private readonly string _wfsUrl;
    private readonly string _paradasLayer;

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public StmService(HttpClient http, IConfiguration config, ILogger<StmService> logger)
    {
        _http = http;
        _logger = logger;
        _endpoint = config["Stm:Endpoint"]
                    ?? "https://www.montevideo.gub.uy/buses/rest/stm-online";
        _wfsUrl = config["Paradas:WfsUrl"]
                    ?? "https://geoserver.montevideo.gub.uy/geoserver/wfs";
        _paradasLayer = config["Paradas:Layer"]
                    ?? "imm:v_uptu_paradas_con_horarios";
    }

    public async Task<IReadOnlyList<Bus>> GetBusesAsync(
        IEnumerable<string>? lineas = null, CancellationToken ct = default)
    {
        var filtro = lineas?
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .ToArray() ?? Array.Empty<string>();

        // {} = todos, {"lineas":[...]} = filtradas
        object payload = filtro.Length > 0 ? new { lineas = filtro } : new { };
        var body = JsonSerializer.Serialize(payload);

        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(_endpoint, content, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var fc = await JsonSerializer.DeserializeAsync<FeatureCollection>(stream, JsonOpts, ct);

        if (fc?.Features is null || fc.Features.Count == 0)
            return Array.Empty<Bus>();

        var buses = new List<Bus>(fc.Features.Count);
        foreach (var f in fc.Features)
        {
            // Necesitamos geometría con [lng, lat] y propiedades válidas.
            if (f.Geometry?.Coordinates is { Length: >= 2 } c && f.Properties is { } p)
            {
                buses.Add(new Bus
                {
                    Id = p.Id,
                    Linea = p.Linea,
                    Sublinea = p.Sublinea,
                    Destino = p.DestinoDesc,
                    TipoLinea = p.TipoLineaDesc,
                    Empresa = p.CodigoEmpresa,
                    CodigoBus = p.CodigoBus,
                    Velocidad = p.Velocidad,
                    Lng = c[0],
                    Lat = c[1]
                });
            }
        }

        _logger.LogInformation("STM devolvió {Count} ómnibus (filtro: {Filtro})",
            buses.Count, filtro.Length > 0 ? string.Join(",", filtro) : "todas");

        return buses;
    }

    public async Task<IReadOnlyList<LineaInfo>> GetLineasAsync(CancellationToken ct = default)
    {
        // Construye el DataProvider a partir de las líneas que están circulando ahora.
        var buses = await GetBusesAsync(null, ct);

        var lineas = buses
            .Where(b => !string.IsNullOrWhiteSpace(b.Linea))
            .GroupBy(b => b.Linea!)
            .Select(g => new LineaInfo
            {
                Linea = g.Key,
                // Texto representativo de la línea (primera sublínea no vacía del grupo).
                Texto = g.Select(x => x.Sublinea)
                         .FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? ""
            })
            // Primero las numéricas en orden ascendente, luego las alfanuméricas (CE1, D5, …).
            .OrderBy(l => int.TryParse(l.Linea, out _) ? 0 : 1)
            .ThenBy(l => int.TryParse(l.Linea, out var n) ? n : int.MaxValue)
            .ThenBy(l => l.Linea, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation("DataProvider: {Count} líneas distintas en circulación", lineas.Count);
        return lineas;
    }

    public async Task<IReadOnlyList<Parada>> GetParadasAsync(
        IEnumerable<string> lineas, CancellationToken ct = default)
    {
        // Sanitiza (solo alfanuméricos) para armar el CQL_FILTER sin riesgo de inyección.
        var ls = lineas
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => new string(l.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant())
            .Where(l => l.Length > 0)
            .Distinct()
            .ToArray();

        if (ls.Length == 0) return Array.Empty<Parada>();

        var inList = string.Join(",", ls.Select(l => $"'{l}'"));
        var cql = $"desc_linea IN ({inList})";
        var url = $"{_wfsUrl}?service=WFS&version=2.0.0&request=GetFeature" +
                  $"&typeNames={Uri.EscapeDataString(_paradasLayer)}" +
                  $"&outputFormat=application/json&srsName=EPSG:4326" +
                  $"&cql_filter={Uri.EscapeDataString(cql)}";

        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var fc = await JsonSerializer.DeserializeAsync<ParadaFeatureCollection>(stream, JsonOpts, ct);

        if (fc?.Features is null || fc.Features.Count == 0)
            return Array.Empty<Parada>();

        // Una línea recorre varias variantes (sentidos); deduplicamos por código de parada.
        var porParada = new Dictionary<int, Parada>();
        foreach (var f in fc.Features)
        {
            if (f.Geometry?.Coordinates is { Length: >= 2 } c && f.Properties is { } p
                && !porParada.ContainsKey(p.CodParada))
            {
                porParada[p.CodParada] = new Parada
                {
                    Cod = p.CodParada,
                    Linea = p.DescLinea,
                    Calle = p.Calle,
                    Esquina = p.Esquina,
                    Lng = c[0],
                    Lat = c[1]
                };
            }
        }

        _logger.LogInformation("Paradas: {Count} para líneas {Lineas}", porParada.Count, string.Join(",", ls));
        return porParada.Values.ToList();
    }
}
