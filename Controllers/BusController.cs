using Microsoft.AspNetCore.Mvc;
using BusEnVivo.Services;

namespace BusEnVivo.Controllers;

/// <summary>
/// API que el mapa consume por AJAX para refrescar las posiciones en vivo.
/// </summary>
[Route("api/[controller]")]
public class BusController : Controller
{
    private readonly IStmService _stm;

    public BusController(IStmService stm) => _stm = stm;

    // GET /api/bus/posiciones
    // GET /api/bus/posiciones?lineas=155,103
    [HttpGet("posiciones")]
    public async Task<IActionResult> Posiciones(string? lineas, CancellationToken ct)
    {
        var filtro = string.IsNullOrWhiteSpace(lineas)
            ? null
            : lineas.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        try
        {
            var buses = await _stm.GetBusesAsync(filtro, ct);
            return Json(new { ok = true, count = buses.Count, buses });
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status502BadGateway;
            return Json(new { ok = false, error = ex.Message });
        }
    }

    // GET /api/bus/lineas  -> DataProvider del <select> (número + texto de cada línea)
    [HttpGet("lineas")]
    public async Task<IActionResult> Lineas(CancellationToken ct)
    {
        try
        {
            var lineas = await _stm.GetLineasAsync(ct);
            return Json(new { ok = true, count = lineas.Count, lineas });
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status502BadGateway;
            return Json(new { ok = false, error = ex.Message });
        }
    }
}
