namespace GestionArchivosEscaneados.Infrastructure.Auth;

public static class UsuarioNormalizador
{
    /// <summary>
    /// Igual que Worker 1: acepta usuario, correo/UPN o DOMINIO\cuenta.
    /// </summary>
    public static string NormalizarIngreso(string entrada)
    {
        if (string.IsNullOrWhiteSpace(entrada))
            return string.Empty;

        var valor = entrada.Trim();
        var arroba = valor.IndexOf('@');
        var local = arroba >= 0 ? valor[..arroba] : valor;

        if (local.Contains('\\'))
            local = local.Split('\\', StringSplitOptions.RemoveEmptyEntries).Last();

        return local.Trim().ToLowerInvariant();
    }

    public static string LimpiarLineaUsuariosTxt(string linea) =>
        linea.Trim().TrimStart('\ufeff').ToLowerInvariant();
}
