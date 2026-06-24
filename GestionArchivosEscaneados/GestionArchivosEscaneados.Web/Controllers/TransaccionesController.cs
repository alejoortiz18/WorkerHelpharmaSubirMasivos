using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Models.ViewModels;
using GestionArchivosEscaneados.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace GestionArchivosEscaneados.Web.Controllers;

[RequireUsuario]
[RequireAdministrador]
public class TransaccionesController : Controller
{
    private readonly TransaccionesAppService _transacciones;

    public TransaccionesController(TransaccionesAppService transacciones)
    {
        _transacciones = transacciones;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var usuarios = await _transacciones.ListarUsuariosAsync(cancellationToken);
        return View(new TransaccionesUsuariosViewModel { Usuarios = usuarios });
    }

    public async Task<IActionResult> Fechas(string usuario, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuario))
            return RedirectToAction(nameof(Index));

        var fechas = await _transacciones.ListarFechasAsync(usuario, cancellationToken);
        return View(new TransaccionesFechasViewModel
        {
            Usuario = usuario.Trim(),
            Fechas = fechas
        });
    }

    public async Task<IActionResult> Documentos(string usuario, string fecha, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(fecha))
            return RedirectToAction(nameof(Index));

        var documentos = await _transacciones.ListarDocumentosAsync(usuario, fecha, cancellationToken);
        return View(new TransaccionesDocumentosViewModel
        {
            Usuario = usuario.Trim(),
            Fecha = fecha.Trim(),
            Documentos = documentos
        });
    }
}
