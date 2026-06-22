namespace GestionArchivosEscaneados.Models.Dto;

public enum ValidacionLoginEstado
{
    Exito,
    BaseDatosNoAccesible,
    UsuarioNoRegistrado
}

public sealed class ValidacionLoginResult
{
    public ValidacionLoginEstado Estado { get; init; }

    public string? UsuarioNormalizado { get; init; }

    public string? OrigenValidacion { get; init; }

    public static ValidacionLoginResult Ok(string usuario, string origen) =>
        new() { Estado = ValidacionLoginEstado.Exito, UsuarioNormalizado = usuario, OrigenValidacion = origen };

    public static ValidacionLoginResult BaseDatosNoDisponible(string origen) =>
        new() { Estado = ValidacionLoginEstado.BaseDatosNoAccesible, OrigenValidacion = origen };

    public static ValidacionLoginResult NoRegistrado(string origen) =>
        new() { Estado = ValidacionLoginEstado.UsuarioNoRegistrado, OrigenValidacion = origen };
}
