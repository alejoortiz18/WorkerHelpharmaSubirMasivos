using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace GestionArchivosEscaneados.Web.Controllers;

[RequireUsuario]
[RequireAdministrador]
public class InformesController : Controller
{
    private readonly InformesAppService _informes;

    public InformesController(InformesAppService informes)
    {
        _informes = informes;
    }

    public async Task<IActionResult> Index(
        string? desde,
        string? hasta,
        string? usuario,
        CancellationToken cancellationToken)
    {
        DateOnly? desdeDate = ParseFecha(desde);
        DateOnly? hastaDate = ParseFecha(hasta);

        var datos = await _informes.ObtenerInformesAsync(desdeDate, hastaDate, usuario, cancellationToken);
        return View(datos);
    }

    private static DateOnly? ParseFecha(string? valor) =>
        DateOnly.TryParseExact(valor, "yyyy-MM-dd", out var fecha) ? fecha : null;
}
