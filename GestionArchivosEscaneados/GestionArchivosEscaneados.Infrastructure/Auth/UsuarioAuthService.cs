using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Infrastructure.Auth;

public class UsuarioAuthService
{
    private const string OrigenValidacion = "dbo.Usuarios.NombreUsuario";
    private readonly ITrazabilidadConsultaSqlService _trazabilidad;
    private readonly ILogger<UsuarioAuthService> _logger;

    public UsuarioAuthService(
        ITrazabilidadConsultaSqlService trazabilidad,
        ILogger<UsuarioAuthService> logger)
    {
        _trazabilidad = trazabilidad;
        _logger = logger;
    }

    public async Task<ValidacionLoginResult> ValidarLoginAsync(
        string usuarioIngresado,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(usuarioIngresado))
            return ValidacionLoginResult.NoRegistrado(OrigenValidacion);

        var ingresoNormalizado = UsuarioNormalizador.NormalizarIngreso(usuarioIngresado);
        if (string.IsNullOrWhiteSpace(ingresoNormalizado))
            return ValidacionLoginResult.NoRegistrado(OrigenValidacion);

        try
        {
            var existe = await _trazabilidad.UsuarioExisteAsync(ingresoNormalizado, cancellationToken);
            if (existe)
            {
                _logger.LogInformation(
                    "LoginExitoso | Usuario={Usuario} | Ingreso={Ingreso}",
                    ingresoNormalizado,
                    usuarioIngresado.Trim());
                return ValidacionLoginResult.Ok(ingresoNormalizado, OrigenValidacion);
            }

            _logger.LogWarning(
                "UsuarioNoRegistrado | Ingreso={Ingreso} | Normalizado={Normalizado} | Origen={Origen}",
                usuarioIngresado.Trim(),
                ingresoNormalizado,
                OrigenValidacion);

            return ValidacionLoginResult.NoRegistrado(OrigenValidacion);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LoginFalloBaseDatos | Origen={Origen}", OrigenValidacion);
            return ValidacionLoginResult.BaseDatosNoDisponible(OrigenValidacion);
        }
    }
}
