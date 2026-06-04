using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Constants;
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

        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        var usuario = await _auth.ValidarLoginAsync(model.Usuario, cancellationToken);
        if (usuario == null)
        {
            model.Error = MensajesUsuario.UsuarioNoRegistrado;
            return View(model);
        }

        HttpContext.Session.SetString(SessionKeys.Usuario, usuario);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction(nameof(Login));
    }
}
