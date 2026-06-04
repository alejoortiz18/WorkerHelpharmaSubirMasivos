using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Models.Entities;
using GestionArchivosEscaneados.Models.ViewModels;
using GestionArchivosEscaneados.Web.Filters;
using Microsoft.AspNetCore.Mvc;

namespace GestionArchivosEscaneados.Web.Controllers;

[RequireUsuario]
public class DocumentosController : Controller
{
    private readonly DashboardAppService _dashboard;
    private readonly ReprocesoAppService _reproceso;

    public DocumentosController(DashboardAppService dashboard, ReprocesoAppService reproceso)
    {
        _dashboard = dashboard;
        _reproceso = reproceso;
    }

    public async Task<IActionResult> Dashboard(string fecha, CancellationToken cancellationToken)
    {
        var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;
        var resumen = await _dashboard.ObtenerResumenAsync(usuario, fecha, cancellationToken);
        if (resumen == null)
            return RedirectToAction("Index", "Home", new { error = MensajesUsuario.FechaNoDisponible });

        return View(new DashboardViewModel
        {
            Fecha = fecha,
            CantidadProcesados = resumen.CantidadProcesados,
            NoProcesados = resumen.NoProcesados
        });
    }

    public IActionResult NoProcesados(string fecha, string? ver)
    {
        var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;
        var archivos = _reproceso.ListarNoProcesados(usuario, fecha);
        var seleccionado = ver ?? archivos.FirstOrDefault()?.NombreArchivo;

        var vm = CrearNoProcesadosViewModel(fecha, archivos, seleccionado);
        if (TempData["MensajeExito"] is string exito)
            vm.MensajeExito = exito;
        if (TempData["MensajeError"] is string error)
            vm.MensajeError = error;
        if (TempData["MensajeAdvertencia"] is string advertencia)
            vm.MensajeAdvertencia = advertencia;

        return View(vm);
    }

    public IActionResult Pdf(string fecha, string archivo)
    {
        var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;
        var ruta = _reproceso.ResolverRutaPdf(usuario, fecha, archivo);
        if (ruta == null)
            return NotFound();

        return PhysicalFile(ruta, "application/pdf", enableRangeProcessing: true);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcesarLote(ProcesarLoteRequest request, CancellationToken cancellationToken)
    {
        var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;
        var documentos = request.Archivos
            .Where(d => !string.IsNullOrWhiteSpace(d.CodigoBarras))
            .Select(d => (d.NombreArchivo, d.CodigoBarras))
            .ToList();

        if (documentos.Count == 0)
        {
            var archivosActuales = _reproceso.ListarNoProcesados(usuario, request.Fecha);
            return View("NoProcesados", CrearNoProcesadosViewModel(
                request.Fecha,
                archivosActuales,
                request.ArchivoSeleccionado,
                request.Archivos,
                mensajeAdvertencia: MensajesUsuario.ReprocesoLoteSinCodigos));
        }

        var resultados = await _reproceso.ReprocesarLoteAsync(
            usuario,
            request.Fecha,
            documentos,
            cancellationToken);

        var exitosos = resultados
            .Where(r => r.Exito)
            .Select(r => r.NombreArchivo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fallidos = resultados
            .Where(r => !r.Exito)
            .ToDictionary(r => r.NombreArchivo, r => r.CodigoBarras, StringComparer.OrdinalIgnoreCase);

        var archivosRestantes = _reproceso.ListarNoProcesados(usuario, request.Fecha)
            .Where(a => !exitosos.Contains(a.NombreArchivo))
            .ToList();

        var exitos = resultados.Count(r => r.Exito);
        var errores = resultados.Count(r => !r.Exito);

        if (errores == 0)
        {
            TempData["MensajeExito"] = string.Format(MensajesUsuario.ReprocesoLoteExitoso, exitos);
            return RedirectToAction(nameof(NoProcesados), new { fecha = request.Fecha });
        }

        var itemsRestantes = archivosRestantes.Select(a => new ArchivoNoProcesadoItemViewModel
        {
            NombreArchivo = a.NombreArchivo,
            Fecha = a.Fecha,
            CodigoBarras = fallidos.GetValueOrDefault(a.NombreArchivo, string.Empty),
            ErrorProceso = fallidos.ContainsKey(a.NombreArchivo)
        }).ToList();

        string? mensajeExito = null;
        string? mensajeError = null;
        string? mensajeAdvertencia = null;

        if (exitos > 0 && errores > 0)
            mensajeAdvertencia = string.Format(MensajesUsuario.ReprocesoLoteParcial, exitos, errores);
        else if (errores > 0)
            mensajeError = MensajesUsuario.DocumentoNoEncontrado;

        var seleccionado = request.ArchivoSeleccionado;
        if (!string.IsNullOrWhiteSpace(seleccionado) &&
            !itemsRestantes.Any(a => a.NombreArchivo == seleccionado))
        {
            seleccionado = itemsRestantes.FirstOrDefault()?.NombreArchivo;
        }

        return View("NoProcesados", new NoProcesadosViewModel
        {
            Fecha = request.Fecha,
            ArchivoSeleccionado = seleccionado,
            Archivos = itemsRestantes,
            MensajeExito = mensajeExito,
            MensajeError = mensajeError,
            MensajeAdvertencia = mensajeAdvertencia
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Eliminar(EliminarDocumentoRequest request, CancellationToken cancellationToken)
    {
        var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;

        if (string.IsNullOrWhiteSpace(request.NombreArchivo))
        {
            TempData["MensajeError"] = MensajesUsuario.EliminarError;
            return RedirectToAction(nameof(NoProcesados), new { fecha = request.Fecha });
        }

        var eliminado = await _reproceso.EliminarAsync(
            usuario,
            request.Fecha,
            request.NombreArchivo,
            cancellationToken);

        TempData[eliminado ? "MensajeExito" : "MensajeError"] =
            eliminado ? MensajesUsuario.EliminarExito : MensajesUsuario.EliminarError;

        return RedirectToAction(nameof(NoProcesados), new { fecha = request.Fecha });
    }

    private static NoProcesadosViewModel CrearNoProcesadosViewModel(
        string fecha,
        IReadOnlyList<ArchivoNoProcesado> archivos,
        string? seleccionado,
        IEnumerable<ArchivoNoProcesadoItemViewModel>? valoresPrevios = null,
        string? mensajeExito = null,
        string? mensajeError = null,
        string? mensajeAdvertencia = null)
    {
        var previos = valoresPrevios?
            .ToDictionary(d => d.NombreArchivo, d => d, StringComparer.OrdinalIgnoreCase);

        return new NoProcesadosViewModel
        {
            Fecha = fecha,
            ArchivoSeleccionado = seleccionado ?? archivos.FirstOrDefault()?.NombreArchivo,
            Archivos = archivos.Select(a =>
            {
                ArchivoNoProcesadoItemViewModel? previo = null;
                previos?.TryGetValue(a.NombreArchivo, out previo);
                return new ArchivoNoProcesadoItemViewModel
                {
                    NombreArchivo = a.NombreArchivo,
                    Fecha = a.Fecha,
                    CodigoBarras = previo?.CodigoBarras ?? string.Empty,
                    ErrorProceso = previo?.ErrorProceso ?? false
                };
            }).ToList(),
            MensajeExito = mensajeExito,
            MensajeError = mensajeError,
            MensajeAdvertencia = mensajeAdvertencia
        };
    }
}
