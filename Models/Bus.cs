using System.Text.Json.Serialization;

namespace BusEnVivo.Models;

/// <summary>
/// Modelo de dominio que la aplicación expone al frontend.
/// Es una versión "limpia" de cada ómnibus, independiente del formato GeoJSON de la STM.
/// </summary>
public class Bus
{
    public string? Id { get; set; }
    public string? Linea { get; set; }
    public string? Sublinea { get; set; }
    public string? Destino { get; set; }
    public string? TipoLinea { get; set; }
    public int Empresa { get; set; }
    public int CodigoBus { get; set; }
    public int Velocidad { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}

/// <summary>
/// Ítem del DataProvider de líneas: número de línea + su texto (sublínea).
/// Alimenta el &lt;select&gt; del filtro.
/// </summary>
public class LineaInfo
{
    public string Linea { get; set; } = "";
    public string Texto { get; set; } = "";
}

// ----------------------------------------------------------------------------
// DTOs para deserializar el GeoJSON que devuelve el endpoint de la STM:
// POST https://www.montevideo.gub.uy/buses/rest/stm-online
// ----------------------------------------------------------------------------

public class FeatureCollection
{
    [JsonPropertyName("features")]
    public List<Feature> Features { get; set; } = new();
}

public class Feature
{
    [JsonPropertyName("properties")]
    public BusProperties? Properties { get; set; }

    [JsonPropertyName("geometry")]
    public Geometry? Geometry { get; set; }
}

public class Geometry
{
    // GeoJSON: [longitud, latitud]
    [JsonPropertyName("coordinates")]
    public double[]? Coordinates { get; set; }
}

public class BusProperties
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("codigoEmpresa")]
    public int CodigoEmpresa { get; set; }

    [JsonPropertyName("codigoBus")]
    public int CodigoBus { get; set; }

    [JsonPropertyName("linea")]
    public string? Linea { get; set; }

    [JsonPropertyName("sublinea")]
    public string? Sublinea { get; set; }

    [JsonPropertyName("tipoLineaDesc")]
    public string? TipoLineaDesc { get; set; }

    [JsonPropertyName("destinoDesc")]
    public string? DestinoDesc { get; set; }

    [JsonPropertyName("velocidad")]
    public int Velocidad { get; set; }
}
