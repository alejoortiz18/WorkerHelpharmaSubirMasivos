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
        var soporteNormalizado = NormalizarSoporte(soporteConsulta);

        var respuesta = await _soporteApi.EnviarSoporteAsync(soporteConsulta);
        if (respuesta == null)
        {
            return new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.FalloApiDatos,
                Soporte = soporteNormalizado
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var enviadoFisico = await _soporteFisicoApi.EnviarSoporteFisicoAsync(
            soporteNormalizado,
            contenidoPdf,
            nombreArchivo,
            respuesta,
            idUsuario);

        if (!enviadoFisico)
        {
            return new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.FalloApiFisico,
                Soporte = soporteNormalizado,
                Datos = respuesta
            };
        }

        _logger.LogInformation(
            "SoporteProcesamientoOK | Soporte={Soporte} | Usuario={Usuario}",
            soporteConsulta,
            idUsuario);

        return new SoporteProcesamientoResult
        {
            Estado = SoporteProcesamientoEstado.Exito,
            Soporte = soporteNormalizado,
            Datos = respuesta
        };
    }

    private static string NormalizarSoporte(string soporte)
    {
        return string.IsNullOrWhiteSpace(soporte)
            ? string.Empty
            : soporte.Trim().Replace("-", string.Empty).Replace(" ", string.Empty);
    }
}
