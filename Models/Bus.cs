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
    public int Variante { get; set; }
    public int Velocidad { get; set; }
    public int SubsistemaCod { get; set; }
    public string? Subsistema { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}

/// <summary>
/// Recorrido (traza) de una variante: sus paradas en orden, para dibujar la ruta.
/// </summary>
public class Recorrido
{
    public int Variante { get; set; }
    public List<Parada> Paradas { get; set; } = new();
}

/// <summary>
/// Ítem del DataProvider de líneas: número de línea + su texto (sublínea).
/// Alimenta el autocompletado del filtro.
/// </summary>
public class LineaInfo
{
    public string Linea { get; set; } = "";
    public string Texto { get; set; } = "";
    public string Subsistema { get; set; } = "";
}

/// <summary>
/// Parada de ómnibus por la que pasa una línea (capa de paradas del mapa).
/// </summary>
public class Parada
{
    public int Cod { get; set; }
    public string? Linea { get; set; }
    public string? Calle { get; set; }
    public string? Esquina { get; set; }
    public double Lat { get; set; }
    public double Lng { get; set; }
}

// ---- DTOs para el GeoJSON de paradas del GeoServer (WFS) ----
public class ParadaFeatureCollection
{
    [JsonPropertyName("features")]
    public List<ParadaFeature> Features { get; set; } = new();
}

public class ParadaFeature
{
    [JsonPropertyName("properties")]
    public ParadaProperties? Properties { get; set; }

    [JsonPropertyName("geometry")]
    public Geometry? Geometry { get; set; }
}

public class ParadaProperties
{
    [JsonPropertyName("cod_ubic_parada")]
    public int CodParada { get; set; }

    [JsonPropertyName("desc_linea")]
    public string? DescLinea { get; set; }

    [JsonPropertyName("cod_variante")]
    public int CodVariante { get; set; }

    [JsonPropertyName("ordinal")]
    public int Ordinal { get; set; }

    [JsonPropertyName("calle")]
    public string? Calle { get; set; }

    [JsonPropertyName("esquina")]
    public string? Esquina { get; set; }
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

    [JsonPropertyName("variante")]
    public int Variante { get; set; }

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

    [JsonPropertyName("subsistema")]
    public int Subsistema { get; set; }

    [JsonPropertyName("subsistemaDesc")]
    public string? SubsistemaDesc { get; set; }
}
