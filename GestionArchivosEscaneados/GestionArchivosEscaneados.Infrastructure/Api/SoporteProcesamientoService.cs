using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Models.Enums;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Infrastructure.Api;

public class SoporteProcesamientoService
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
        string rutaArchivoPdf,
        string idUsuario,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var respuesta = await _soporteApi.EnviarSoporteAsync(soporte);
        if (respuesta == null)
        {
            return new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.FalloApiDatos,
                Soporte = soporte
            };
        }

        cancellationToken.ThrowIfCancellationRequested();

        var enviadoFisico = await _soporteFisicoApi.EnviarSoporteFisicoAsync(
            soporte,
            rutaArchivoPdf,
            respuesta,
            idUsuario);

        if (!enviadoFisico)
        {
            return new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.FalloApiFisico,
                Soporte = soporte,
                Datos = respuesta
            };
        }

        _logger.LogInformation(
            "SoporteProcesamientoOK | Soporte={Soporte} | Usuario={Usuario}",
            soporte,
            idUsuario);

        return new SoporteProcesamientoResult
        {
            Estado = SoporteProcesamientoEstado.Exito,
            Soporte = soporte,
            Datos = respuesta
        };
    }
}
