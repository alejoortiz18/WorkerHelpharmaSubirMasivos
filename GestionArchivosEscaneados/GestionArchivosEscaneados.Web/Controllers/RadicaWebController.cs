using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace GestionArchivosEscaneados.Web.Controllers;

[RequireUsuario]
[RequireAdministrador]
public class RadicaWebController : Controller
{
    private readonly RadicaWebAppService _radicaWeb;

    public RadicaWebController(RadicaWebAppService radicaWeb)
    {
        _radicaWeb = radicaWeb;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? desde,
        string? hasta,
        string? usuario,
        string? bodega,
        string? estado,
        int pagina = 1,
        int tamanoPagina = 20,
        string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var filtros = new RadicaWebFiltrosConsulta
        {
            Desde = ParseFecha(desde),
            Hasta = ParseFecha(hasta),
            Usuario = string.IsNullOrWhiteSpace(usuario) ? null : usuario.Trim(),
            Bodega = string.IsNullOrWhiteSpace(bodega) ? null : bodega.Trim(),
            Success = ParseEstado(estado),
            Pagina = pagina,
            TamanoPagina = tamanoPagina,
            Tab = string.IsNullOrWhiteSpace(tab) ? "notificaciones" : tab.Trim().ToLowerInvariant()
        };

        var panel = await _radicaWeb.ObtenerPanelAsync(filtros, cancellationToken);
        return View(panel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Renotificar(
        long id,
        string? desde,
        string? hasta,
        string? usuario,
        string? bodega,
        string? estado,
        int pagina = 1,
        int tamanoPagina = 20,
        string? tab = null,
        CancellationToken cancellationToken = default)
    {
        var resultado = await _radicaWeb.RenotificarAsync(id, cancellationToken);

        TempData[resultado.Exito ? "RadicaWebMensajeOk" : "RadicaWebMensajeError"] = resultado.Mensaje;

        return RedirectToAction(nameof(Index), new
        {
            desde,
            hasta,
            usuario,
            bodega,
            estado,
            pagina,
            tamanoPagina,
            tab = string.IsNullOrWhiteSpace(tab) ? "notificaciones" : tab
        });
    }

    private static DateOnly? ParseFecha(string? valor) =>
        DateOnly.TryParseExact(valor, "yyyy-MM-dd", out var fecha) ? fecha : null;

    private static bool? ParseEstado(string? estado) =>
        estado switch
        {
            "exitosas" => true,
            "fallidas" => false,
            _ => null
        };
}
