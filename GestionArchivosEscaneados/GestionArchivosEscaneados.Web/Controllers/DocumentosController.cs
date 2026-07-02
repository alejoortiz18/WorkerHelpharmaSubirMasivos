using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Models.Entities;
using GestionArchivosEscaneados.Models.Enums;
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

    public async Task<IActionResult> NoProcesados(string fecha, string? ver, CancellationToken cancellationToken)
    {
        var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;
        var archivos = await _reproceso.ListarNoProcesadosAsync(usuario, fecha, cancellationToken);
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
        var contenido = _reproceso.LeerPdfNoProcesado(usuario, fecha, archivo);
        if (contenido == null)
            return NotFound();

        return File(contenido, "application/pdf");
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
            var archivosActuales = await _reproceso.ListarNoProcesadosAsync(usuario, request.Fecha, cancellationToken);
            return View("NoProcesados", CrearNoProcesadosViewModel(
                request.Fecha,
                archivosActuales,
                request.ArchivoSeleccionado,
                request.Archivos,
                mensajeAdvertencia: MensajesUsuario.ReprocesoLoteSinCodigos));
        }

        var resultados = new List<ReprocesoLoteItemResult>();
        foreach (var (nombreArchivo, codigoBarras) in documentos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            resultados.Add(await _reproceso.ProcesarItemAsync(
                usuario,
                request.Fecha,
                nombreArchivo,
                codigoBarras,
                cancellationToken));
        }

        var exitosos = resultados
            .Where(r => r.Exito)
            .Select(r => r.NombreArchivo)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var fallidos = resultados
            .Where(r => !r.Exito)
            .ToDictionary(r => r.NombreArchivo, r => r.CodigoBarras, StringComparer.OrdinalIgnoreCase);

        var archivosRestantes = (await _reproceso.ListarNoProcesadosAsync(usuario, request.Fecha, cancellationToken))
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
            mensajeError = ResolverMensajeError(resultados);

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

    private static string ResolverMensajeError(IReadOnlyCollection<ReprocesoLoteItemResult> resultados)
    {
        var estados = resultados
            .Where(r => !r.Exito)
            .Select(r => r.Estado)
            .Distinct()
            .ToList();

        if (estados.Count == 1)
        {
            return estados[0] switch
            {
                SoporteProcesamientoEstado.FalloApiDatos => MensajesUsuario.ErrorConsultaDatosSoportes,
                SoporteProcesamientoEstado.FalloApiFisico => MensajesUsuario.ErrorEnvioSoporteFisico,
                SoporteProcesamientoEstado.FalloBarcode => MensajesUsuario.ErrorLecturaBarcode,
                SoporteProcesamientoEstado.FalloOpenAi => MensajesUsuario.ErrorOpenAi,
                _ => MensajesUsuario.DocumentoNoEncontrado
            };
        }

        return MensajesUsuario.DocumentoNoEncontrado;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ProcesarDocumento(ProcesarDocumentoRequest request, CancellationToken cancellationToken)
    {
        var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;

        if (string.IsNullOrWhiteSpace(request.NombreArchivo) || string.IsNullOrWhiteSpace(request.CodigoBarras))
        {
            return BadRequest(new
            {
                exito = false,
                estado = SoporteProcesamientoEstado.ErrorInesperado.ToString()
            });
        }

        var result = await _reproceso.ProcesarItemAsync(
            usuario,
            request.Fecha,
            request.NombreArchivo,
            request.CodigoBarras,
            cancellationToken);

        return Json(new
        {
            exito = result.Exito,
            estado = result.Estado.ToString(),
            nombreArchivo = result.NombreArchivo
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReprocesarDocumento(ReprocesarDocumentoRequest request, CancellationToken cancellationToken)
    {
        var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;

        if (string.IsNullOrWhiteSpace(request.NombreArchivo))
        {
            return BadRequest(new
            {
                exito = false,
                estado = SoporteProcesamientoEstado.ErrorInesperado.ToString()
            });
        }

        var estado = await _reproceso.ReprocesarAsync(
            usuario,
            request.Fecha,
            request.NombreArchivo,
            string.Empty,
            cancellationToken);

        return Json(new
        {
            exito = estado == SoporteProcesamientoEstado.Exito,
            estado = estado.ToString()
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
            TotalConIntentoPrevio = archivos.Count(a => a.TieneIntentoPrevio),
            TotalPendientesReproceso = archivos.Count(a => !a.TieneIntentoPrevio),
            Archivos = archivos.Select(a =>
            {
                ArchivoNoProcesadoItemViewModel? previo = null;
                previos?.TryGetValue(a.NombreArchivo, out previo);
                return new ArchivoNoProcesadoItemViewModel
                {
                    NombreArchivo = a.NombreArchivo,
                    Fecha = a.Fecha,
                    FechaFactura = a.FechaFactura,
                    CodigoBarras = previo?.CodigoBarras ?? string.Empty,
                    ErrorProceso = previo?.ErrorProceso ?? false,
                    TieneIntentoPrevio = a.TieneIntentoPrevio
                };
            }).ToList(),
            MensajeExito = mensajeExito,
            MensajeError = mensajeError,
            MensajeAdvertencia = mensajeAdvertencia
        };
    }
}
