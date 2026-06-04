using GestionArchivosEscaneados.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace GestionArchivosEscaneados.Web.Filters;

public class RequireUsuarioAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var usuario = context.HttpContext.Session.GetString(SessionKeys.Usuario);
        if (string.IsNullOrWhiteSpace(usuario))
        {
            context.Result = new RedirectToActionResult("Login", "Account", null);
            return;
        }

        base.OnActionExecuting(context);
    }
}
