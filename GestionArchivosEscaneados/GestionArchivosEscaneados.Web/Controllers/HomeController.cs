using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Models.ViewModels;
using GestionArchivosEscaneados.Web.Filters;
using GestionArchivosEscaneados.Web.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace GestionArchivosEscaneados.Web.Controllers;

public class HomeController : Controller
{
    private readonly CalendarioAppService _calendario;

    public HomeController(CalendarioAppService calendario)
    {
        _calendario = calendario;
    }

    [RequireUsuario]
    public async Task<IActionResult> Index(int? anio, int? mes, string? error, CancellationToken cancellationToken)
    {
        var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;
        var hoy = DateTime.Today;
        var anioSel = anio ?? hoy.Year;
        var mesSel = mes ?? hoy.Month;

        if (mesSel < 1) { mesSel = 12; anioSel--; }
        if (mesSel > 12) { mesSel = 1; anioSel++; }

        var fechas = await _calendario.ObtenerFechasDisponiblesAsync(usuario, cancellationToken);
        var resumenMes = await _calendario.ObtenerResumenMesAsync(usuario, anioSel, mesSel, cancellationToken);

        return View(new CalendarioViewModel
        {
            Usuario = usuario,
            Anio = anioSel,
            Mes = mesSel,
            FechasDisponibles = fechas.ToHashSet(StringComparer.Ordinal),
            ResumenPorFecha = resumenMes.ToDictionary(r => r.Fecha, StringComparer.Ordinal),
            Error = error
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequireUsuario]
    public async Task<IActionResult> SeleccionarFecha(int anio, int mes, int dia, CancellationToken cancellationToken)
    {
        var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;
        var fecha = new DateTime(anio, mes, dia).ToString("yyyy-MM-dd");

        if (!await _calendario.FechaExisteAsync(usuario, fecha, cancellationToken))
        {
            return RedirectToAction(nameof(Index), new
            {
                anio,
                mes,
                error = MensajesUsuario.FechaNoDisponible
            });
        }

        return RedirectToAction("Dashboard", "Documentos", new { fecha });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
