using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Infrastructure.Salud;
using GestionArchivosEscaneados.Web.Filters;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace GestionArchivosEscaneados.Web.Controllers;

[RequireUsuario]
[RequireAdministrador]
public class SaludController : Controller
{
    private readonly SaludAppService _salud;
    private readonly IHostApplicationLifetime _lifetime;

    public SaludController(SaludAppService salud, IHostApplicationLifetime lifetime)
    {
        _salud = salud;
        _lifetime = lifetime;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var panel = await _salud.ObtenerPanelAsync(cancellationToken);
        panel = panel with { RequiereReinicio = TempData.ContainsKey("SaludRequiereReinicio") };
        return View(panel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Verificar(CancellationToken cancellationToken)
    {
        var panel = await _salud.VerificarAsync(cancellationToken);
        panel = panel with { RequiereReinicio = TempData.ContainsKey("SaludRequiereReinicio") };
        return View("Index", panel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Guardar(
        string id,
        string? endpoint,
        string? endpointVerificacion,
        string? claveCredencial,
        string? valorAdicional,
        string? prompt,
        string? descripcion,
        CancellationToken cancellationToken)
    {
        await _salud.GuardarIntegracionAsync(
            new SaludIntegracionGuardarRequest
            {
                Id = id,
                Endpoint = endpoint,
                EndpointVerificacion = endpointVerificacion,
                ClaveCredencial = claveCredencial,
                ValorAdicional = valorAdicional,
                Prompt = prompt,
                Descripcion = descripcion
            },
            cancellationToken);

        TempData["SaludMensaje"] = EtiquetasUi.ConfiguracionGuardada;
        TempData["SaludRequiereReinicio"] = true;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AplicarCambios()
    {
        TempData["SaludMensaje"] = EtiquetasUi.ReinicioEnCurso;
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            _lifetime.StopApplication();
        });

        return RedirectToAction(nameof(Index));
    }
}
