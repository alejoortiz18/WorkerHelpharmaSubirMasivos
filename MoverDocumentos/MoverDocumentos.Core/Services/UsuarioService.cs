using System.Security.Claims;
using System.Security.Principal;
using Microsoft.Extensions.Logging;

namespace MoverDocumentos.Core.Services;

public class UsuarioService
{
    private readonly ILogger<UsuarioService> _logger;
    private string? _usuarioCache;

    public UsuarioService(ILogger<UsuarioService> logger)
    {
        _logger = logger;
    }

    public string ObtenerUsuarioNormalizado()
    {
        if (!string.IsNullOrEmpty(_usuarioCache))
            return _usuarioCache;

        var identidadBruta = ObtenerIdentidadBruta();
        _usuarioCache = NormalizarDesdeCorreo(identidadBruta);

        _logger.LogInformation(
            "UsuarioDetectado | Usuario={Usuario}",
            _usuarioCache);

        return _usuarioCache;
    }

    /// <summary>Normaliza correo/UPN o nombre de cuenta a la parte local en minúsculas.</summary>
    public static string NormalizarDesdeCorreo(string entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            throw new InvalidOperationException("No se pudo determinar el usuario de Windows.");

        var valor = entrada.Trim();
        var arroba = valor.IndexOf('@');
        var local = arroba >= 0 ? valor[..arroba] : valor;

        if (local.Contains('\\'))
            local = local.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last();

        return local.ToLowerInvariant();
    }

    private static string ObtenerIdentidadBruta()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("MoverDocumentos solo opera en Windows.");

        using var identity = WindowsIdentity.GetCurrent();

        var upn = identity.Claims.FirstOrDefault(c =>
            c.Type == ClaimTypes.Upn ||
            string.Equals(c.Type, "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn", StringComparison.OrdinalIgnoreCase))
            ?.Value;

        if (!string.IsNullOrWhiteSpace(upn))
            return upn;

        var name = identity.Name;
        if (!string.IsNullOrWhiteSpace(name))
            return name;

        var userName = Environment.UserName;
        if (!string.IsNullOrWhiteSpace(userName))
            return userName;

        throw new InvalidOperationException("No se pudo obtener UPN ni nombre de usuario de Windows.");
    }
}
