using BusEnVivo.Models;

namespace BusEnVivo.Services;

/// <summary>
/// Acceso a los horarios interurbanos de urubus.com.uy.
///
/// La página /es/omnibus-horarios renderiza los resultados server-side (sin auth ni token),
/// así que se consume por GET y se parsea el HTML. El catálogo de terminales sale de los
/// arrays JS from_cities / to_cities embebidos en esa misma página.
/// Uso personal/educativo: con caché para no golpear el origen y User-Agent identificable.
/// </summary>
public interface IUrubusService
{
    /// <summary>
    /// Catálogo de terminales (orígenes y destinos) para poblar los autocompletados.
    /// Cacheado con TTL largo (las terminales casi no cambian).
    /// </summary>
    Task<CatalogoTerminales> GetTerminalesAsync(CancellationToken ct = default);

    /// <summary>
    /// Busca servicios entre dos terminales para una fecha. Resultado cacheado por
    /// (origen, destino, fecha, asientos) con TTL corto.
    /// </summary>
    /// <param name="fromId">Id de terminal de origen (ter_from).</param>
    /// <param name="toId">Id de terminal de destino (ter_to).</param>
    /// <param name="fecha">Fecha del viaje.</param>
    /// <param name="asientos">Cantidad de asientos (por defecto 1).</param>
    Task<BusquedaHorarios> BuscarAsync(
        string fromId, string toId, DateOnly fecha, int asientos = 1, CancellationToken ct = default);
}
