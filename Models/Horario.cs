namespace BusEnVivo.Models;

/// <summary>
/// Terminal de ómnibus (origen o destino) del catálogo de urubus.
/// Sale del array JS from_cities / to_cities embebido en la página.
/// </summary>
public class Terminal
{
    public string Id { get; set; } = "";
    public string Nombre { get; set; } = "";
}

/// <summary>
/// Catálogo de terminales: orígenes y destinos disponibles para la búsqueda.
/// </summary>
public class CatalogoTerminales
{
    public IReadOnlyList<Terminal> Origenes { get; set; } = Array.Empty<Terminal>();
    public IReadOnlyList<Terminal> Destinos { get; set; } = Array.Empty<Terminal>();
}

/// <summary>
/// Un servicio interurbano (una fila de resultados): empresa, horarios, precio, etc.
/// Versión limpia de cada &lt;div class="booking-item-container"&gt; de urubus.
/// </summary>
public class Servicio
{
    public string Empresa { get; set; } = "";
    public string NumeroServicio { get; set; } = "";
    public string Categoria { get; set; } = "";     // DIRECTO, DE LINEA, …
    public string Salida { get; set; } = "";         // HH:mm
    public string Llegada { get; set; } = "";        // HH:mm
    public string Origen { get; set; } = "";
    public string Destino { get; set; } = "";
    public string Duracion { get; set; } = "";       // ej. "2h 05m"
    public string Precio { get; set; } = "";         // ej. "466" (moneda según urubus)
    public int Asientos { get; set; }                // asientos disponibles
    public string Comodidad { get; set; } = "";
    public string Ruta { get; set; } = "";           // detalle del recorrido / coche
    public string FechaHoraSalida { get; set; } = "";// "yyyy-MM-dd HH:mm" cuando se pudo parsear
}

/// <summary>
/// Resultado de una búsqueda de horarios interurbanos.
/// </summary>
public class BusquedaHorarios
{
    public string Origen { get; set; } = "";
    public string Destino { get; set; } = "";
    public DateOnly Fecha { get; set; }
    public IReadOnlyList<Servicio> Servicios { get; set; } = Array.Empty<Servicio>();
    /// <summary>URL de la búsqueda en urubus, para enlazar a la compra real.</summary>
    public string UrlFuente { get; set; } = "";
}
