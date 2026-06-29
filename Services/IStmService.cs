using BusEnVivo.Models;

namespace BusEnVivo.Services;

/// <summary>
/// Abstracción del acceso al sistema de transporte en tiempo real de Montevideo (STM).
/// </summary>
public interface IStmService
{
    /// <summary>
    /// Obtiene la posición en tiempo real de los ómnibus.
    /// </summary>
    /// <param name="lineas">
    /// Líneas a filtrar (por ej. "155", "103"). Si es null o vacío se devuelven todas.
    /// </param>
    /// <param name="subsistema">
    /// Subsistema STM: -1=todos (Montevideo, Canelones, San José, Metropolitano), 1=Montevideo,
    /// 2=Canelones, 3=San José, 4=Metropolitano. Sin este parámetro el endpoint solo da Montevideo.
    /// </param>
    Task<IReadOnlyList<Bus>> GetBusesAsync(IEnumerable<string>? lineas = null, int subsistema = -1, CancellationToken ct = default);

    /// <summary>
    /// DataProvider de líneas: lista distinta de líneas en circulación (número + texto),
    /// para poblar el autocompletado del filtro.
    /// </summary>
    Task<IReadOnlyList<LineaInfo>> GetLineasAsync(CancellationToken ct = default);

    /// <summary>
    /// Paradas por las que pasan las líneas indicadas (capa de paradas del mapa).
    /// </summary>
    Task<IReadOnlyList<Parada>> GetParadasAsync(IEnumerable<string> lineas, CancellationToken ct = default);

    /// <summary>
    /// Recorridos (trazas) de las variantes indicadas: paradas en orden para dibujar la ruta.
    /// </summary>
    Task<IReadOnlyList<Recorrido>> GetRecorridosAsync(IEnumerable<int> variantes, CancellationToken ct = default);
}
