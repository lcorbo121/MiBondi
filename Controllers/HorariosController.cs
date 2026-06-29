using Microsoft.AspNetCore.Mvc;
using BusEnVivo.Services;

namespace BusEnVivo.Controllers;

/// <summary>
/// API de horarios interurbanos (urubus). El frontend la consume por AJAX:
/// catálogo de terminales para los autocompletados y búsqueda de servicios.
/// </summary>
[Route("api/[controller]")]
public class HorariosController : Controller
{
    private readonly IUrubusService _urubus;

    public HorariosController(IUrubusService urubus) => _urubus = urubus;

    // GET /api/horarios/terminales  -> orígenes y destinos para los combobox
    [HttpGet("terminales")]
    public async Task<IActionResult> Terminales(CancellationToken ct)
    {
        try
        {
            var cat = await _urubus.GetTerminalesAsync(ct);
            return Json(new { ok = true, origenes = cat.Origenes, destinos = cat.Destinos });
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status502BadGateway;
            return Json(new { ok = false, error = ex.Message });
        }
    }

    // GET /api/horarios?from=83&to=101&fecha=2026-07-04&seats=1
    [HttpGet]
    public async Task<IActionResult> Buscar(string? from, string? to, DateOnly? fecha, int seats, CancellationToken ct)
    {
        // --- Validación de entrada ---
        if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
            return Json(new { ok = false, error = "Indicá origen (from) y destino (to)." });

        from = new string(from.Where(char.IsDigit).ToArray());
        to = new string(to.Where(char.IsDigit).ToArray());
        if (from.Length == 0 || to.Length == 0)
            return Json(new { ok = false, error = "Los IDs de terminal deben ser numéricos." });
        if (from == to)
            return Json(new { ok = false, error = "El origen y el destino no pueden ser iguales." });

        if (fecha is null)
            return Json(new { ok = false, error = "Indicá la fecha (fecha=yyyy-MM-dd)." });
        if (fecha.Value < DateOnly.FromDateTime(DateTime.Today))
            return Json(new { ok = false, error = "La fecha no puede ser anterior a hoy." });

        var asientos = Math.Clamp(seats <= 0 ? 1 : seats, 1, 10);

        try
        {
            var r = await _urubus.BuscarAsync(from, to, fecha.Value, asientos, ct);
            return Json(new
            {
                ok = true,
                count = r.Servicios.Count,
                origen = r.Origen,
                destino = r.Destino,
                fecha = r.Fecha.ToString("yyyy-MM-dd"),
                urlFuente = r.UrlFuente,
                servicios = r.Servicios
            });
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status502BadGateway;
            return Json(new { ok = false, error = ex.Message });
        }
    }
}
