using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BusEnVivo.Models;

namespace BusEnVivo.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    // Página de horarios interurbanos (consume /api/horarios).
    public IActionResult Interurbano()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
