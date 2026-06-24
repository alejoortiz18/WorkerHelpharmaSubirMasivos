namespace GestionArchivosEscaneados.Constants;

public static class AdministradorPortal
{
    public const string UsuarioAdministrador = "alejandro.ortiz";

    public static bool EsAdministrador(string? usuarioNormalizado) =>
        string.Equals(usuarioNormalizado, UsuarioAdministrador, StringComparison.OrdinalIgnoreCase);
}
