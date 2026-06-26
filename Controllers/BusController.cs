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

    // GET /api/bus/lineas  -> DataProvider del autocompletado (número + texto de cada línea)
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

    // GET /api/bus/paradas?lineas=155,103  -> paradas por las que pasan esas líneas
    [HttpGet("paradas")]
    public async Task<IActionResult> Paradas(string? lineas, CancellationToken ct)
    {
        var filtro = string.IsNullOrWhiteSpace(lineas)
            ? Array.Empty<string>()
            : lineas.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (filtro.Length == 0)
            return Json(new { ok = true, count = 0, paradas = Array.Empty<object>() });

        try
        {
            var paradas = await _stm.GetParadasAsync(filtro, ct);
            return Json(new { ok = true, count = paradas.Count, paradas });
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status502BadGateway;
            return Json(new { ok = false, error = ex.Message });
        }
    }

    // GET /api/bus/recorridos?variantes=9192,9193  -> traza de cada variante (paradas en orden)
    [HttpGet("recorridos")]
    public async Task<IActionResult> Recorridos(string? variantes, CancellationToken ct)
    {
        var vs = (variantes ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => int.TryParse(s, out var n) ? n : 0)
            .Where(n => n > 0)
            .ToArray();

        if (vs.Length == 0)
            return Json(new { ok = true, count = 0, recorridos = Array.Empty<object>() });

        try
        {
            var recorridos = await _stm.GetRecorridosAsync(vs, ct);
            return Json(new { ok = true, count = recorridos.Count, recorridos });
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status502BadGateway;
            return Json(new { ok = false, error = ex.Message });
        }
    }
}
