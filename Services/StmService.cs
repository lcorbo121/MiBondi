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
    private readonly string _variantesLayer;

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
        _variantesLayer = config["Paradas:LayerVariantes"]
                    ?? "imm:Paradas_variantes_all";
    }

    public async Task<IReadOnlyList<Bus>> GetBusesAsync(
        IEnumerable<string>? lineas = null, int subsistema = -1, CancellationToken ct = default)
    {
        var filtro = lineas?
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim())
            .ToArray() ?? Array.Empty<string>();

        // El endpoint, sin "subsistema", responde SOLO Montevideo (subsistema 1) y deja afuera
        // Canelones/San José/Metropolitano (líneas como Z4, 2K). subsistema=-1 trae todos.
        // {"subsistema":-1} = todos los subsistemas; +lineas[...] filtra esas líneas.
        object payload = filtro.Length > 0
            ? new { subsistema, lineas = filtro }
            : new { subsistema };
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
                    Variante = p.Variante,
                    Velocidad = p.Velocidad,
                    SubsistemaCod = p.Subsistema,
                    Subsistema = p.SubsistemaDesc,
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
        // Construye el DataProvider a partir de las líneas que están circulando ahora
        // (todos los subsistemas: Montevideo, Canelones, San José, Metropolitano).
        var buses = await GetBusesAsync(null, -1, ct);

        var lineas = buses
            .Where(b => !string.IsNullOrWhiteSpace(b.Linea))
            // Un mismo código de línea puede existir en distintos subsistemas: separamos por ambos.
            .GroupBy(b => new { Linea = b.Linea!, Subsistema = b.Subsistema ?? "" })
            .Select(g => new LineaInfo
            {
                Linea = g.Key.Linea,
                Subsistema = g.Key.Subsistema,
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

    public async Task<IReadOnlyList<Recorrido>> GetRecorridosAsync(
        IEnumerable<int> variantes, CancellationToken ct = default)
    {
        var vs = variantes.Distinct().Where(v => v > 0).ToArray();
        if (vs.Length == 0) return Array.Empty<Recorrido>();

        var inList = string.Join(",", vs);   // enteros: seguro para el CQL
        var cql = $"cod_variante IN ({inList})";
        var url = $"{_wfsUrl}?service=WFS&version=2.0.0&request=GetFeature" +
                  $"&typeNames={Uri.EscapeDataString(_variantesLayer)}" +
                  $"&outputFormat=application/json&srsName=EPSG:4326" +
                  $"&cql_filter={Uri.EscapeDataString(cql)}";

        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        var fc = await JsonSerializer.DeserializeAsync<ParadaFeatureCollection>(stream, JsonOpts, ct);
        if (fc?.Features is null || fc.Features.Count == 0)
            return Array.Empty<Recorrido>();

        // Agrupa por variante y ordena las paradas por su orden en el recorrido (ordinal).
        var recorridos = fc.Features
            .Where(f => f.Properties is not null && f.Geometry?.Coordinates is { Length: >= 2 })
            .GroupBy(f => f.Properties!.CodVariante)
            .Select(g => new Recorrido
            {
                Variante = g.Key,
                Paradas = g.OrderBy(f => f.Properties!.Ordinal)
                    .Select(f => new Parada
                    {
                        Cod = f.Properties!.CodParada,
                        Calle = f.Properties.Calle,
                        Esquina = f.Properties.Esquina,
                        Lng = f.Geometry!.Coordinates![0],
                        Lat = f.Geometry.Coordinates[1]
                    })
                    .ToList()
            })
            .ToList();

        _logger.LogInformation("Recorridos: {Count} variantes ({Vs})", recorridos.Count, inList);
        return recorridos;
    }
}
