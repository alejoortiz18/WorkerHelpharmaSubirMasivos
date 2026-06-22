using System.Security.Claims;
using System.Security.Principal;
using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Infrastructure.Auth;
using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace GestionArchivosEscaneados.Web.Controllers;

public class AccountController : Controller
{
    private readonly AuthAppService _auth;

    public AccountController(AuthAppService auth)
    {
        _auth = auth;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (!string.IsNullOrWhiteSpace(HttpContext.Session.GetString(SessionKeys.Usuario)))
            return RedirectToAction("Index", "Home");

        return View(new LoginViewModel
        {
            Usuario = ObtenerSugerenciaUsuarioWindows() ?? string.Empty
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        var resultado = await _auth.ValidarLoginAsync(model.Usuario, cancellationToken);

        if (resultado.Estado == ValidacionLoginEstado.Exito &&
            !string.IsNullOrWhiteSpace(resultado.UsuarioNormalizado))
        {
            HttpContext.Session.SetString(SessionKeys.Usuario, resultado.UsuarioNormalizado);
            return RedirectToAction("Index", "Home");
        }

        model.Error = resultado.Estado == ValidacionLoginEstado.BaseDatosNoAccesible
            ? MensajesUsuario.BaseDatosNoAccesible
            : MensajesUsuario.UsuarioNoRegistrado;

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }

    private static string? ObtenerSugerenciaUsuarioWindows()
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var upn = identity.Claims.FirstOrDefault(c =>
                c.Type == ClaimTypes.Upn ||
                string.Equals(c.Type, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn", StringComparison.OrdinalIgnoreCase))
                ?.Value;

            if (!string.IsNullOrWhiteSpace(upn))
                return UsuarioNormalizador.NormalizarIngreso(upn);

            if (!string.IsNullOrWhiteSpace(identity.Name))
                return UsuarioNormalizador.NormalizarIngreso(identity.Name);

            if (!string.IsNullOrWhiteSpace(Environment.UserName))
                return UsuarioNormalizador.NormalizarIngreso(Environment.UserName);
        }
        catch
        {
            return null;
        }

        return null;
    }
}
