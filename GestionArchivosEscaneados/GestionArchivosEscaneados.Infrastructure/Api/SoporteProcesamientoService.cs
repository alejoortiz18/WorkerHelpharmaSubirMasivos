using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Models.Enums;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Infrastructure.Api;

public interface ISoporteProcesamientoService
{
    Task<SoporteProcesamientoResult> ProcesarAsync(
        string soporte,
        byte[] contenidoPdf,
        string nombreArchivo,
        string idUsuario,
        CancellationToken cancellationToken = default);
}

public class SoporteProcesamientoService : ISoporteProcesamientoService
{
    private readonly SoporteApiService _soporteApi;
    private readonly SoporteFisicoApiService _soporteFisicoApi;
    private readonly ILogger<SoporteProcesamientoService> _logger;

    public SoporteProcesamientoService(
        SoporteApiService soporteApi,
        SoporteFisicoApiService soporteFisicoApi,
        ILogger<SoporteProcesamientoService> logger)
    {
        _soporteApi = soporteApi;
        _soporteFisicoApi = soporteFisicoApi;
        _logger = logger;
    }

    public async Task<SoporteProcesamientoResult> ProcesarAsync(
        string soporte,
        byte[] contenidoPdf,
        string nombreArchivo,
        string idUsuario,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var soporteConsulta = soporte.Trim();

        var (respuesta, soporteResuelto) = await ConsultarDatosSoporteAsync(soporteConsulta, cancellationToken);
        if (respuesta == null)
        {
            return new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.FalloApiDatos,
                Soporte = soporteConsulta
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var enviadoFisico = await _soporteFisicoApi.EnviarSoporteFisicoAsync(
            soporteResuelto,
            contenidoPdf,
            nombreArchivo,
            respuesta,
            idUsuario);

        if (!enviadoFisico)
        {
            return new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.FalloApiFisico,
                Soporte = soporteResuelto,
                Datos = respuesta
            };
        }

        _logger.LogInformation(
            "SoporteProcesamientoOK | Soporte={Soporte} | Usuario={Usuario}",
            soporteResuelto,
            idUsuario);

        return new SoporteProcesamientoResult
        {
            Estado = SoporteProcesamientoEstado.Exito,
            Soporte = soporteResuelto,
            Datos = respuesta
        };
    }

    private async Task<(SoporteResponseDto? Datos, string SoporteResuelto)> ConsultarDatosSoporteAsync(
        string soporteConsulta,
        CancellationToken cancellationToken)
    {
        foreach (var candidato in SoporteCodigoOcrHelper.VariantesConfusionI1(soporteConsulta).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var respuesta = await _soporteApi.EnviarSoporteAsync(candidato);
            if (respuesta == null)
                continue;

            if (!string.Equals(candidato, soporteConsulta, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "ApiSoporteOcrCorreccion | SoporteLeido={SoporteLeido} | SoporteResuelto={SoporteResuelto}",
                    soporteConsulta,
                    candidato);
            }

            return (respuesta, candidato);
        }

        return (null, soporteConsulta);
    }
}
