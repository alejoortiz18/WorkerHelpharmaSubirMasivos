using GestionArchivosEscaneados.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GestionArchivosEscaneados.Web.Filters;

/// <summary>
/// Restringe el acceso a la vista de administración (Transacciones) al usuario configurado.
/// </summary>
public class RequireAdministradorAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var usuario = context.HttpContext.Session.GetString(SessionKeys.Usuario);
        if (!AdministradorPortal.EsAdministrador(usuario))
            context.Result = new RedirectToActionResult("Index", "Home", null);

        base.OnActionExecuting(context);
    }
}
