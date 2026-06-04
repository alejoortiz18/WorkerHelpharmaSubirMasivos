using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Models.Dto;

namespace Infrastructure;

/// <summary>
/// Log diario acumulativo en {usuario}\{fecha}\log\{fecha}.txt (RF-11).
/// </summary>
public class LogDiarioService
{
    private static readonly Regex RegexProcesados =
        new(@"^CantidadProcesados:(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RegexNoProcesados =
        new(@"^NoProcesados:(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ILogger<LogDiarioService> _logger;

    public LogDiarioService(ILogger<LogDiarioService> logger)
    {
        _logger = logger;
    }

    public async Task IncrementarAsync(
        RutasLoteContext contexto,
        int procesadosDelta,
        int noProcesadosDelta,
        CancellationToken cancellationToken = default)
    {
        if (procesadosDelta == 0 && noProcesadosDelta == 0)
            return;

        var rutaLog = contexto.RutaLogDiario;
        var (procesados, noProcesados) = await LeerContadoresAsync(rutaLog, cancellationToken);

        procesados += procesadosDelta;
        noProcesados += noProcesadosDelta;

        var contenido =
            $"CantidadProcesados:{procesados}{Environment.NewLine}" +
            $"NoProcesados:{noProcesados}{Environment.NewLine}";

        await File.WriteAllTextAsync(rutaLog, contenido, cancellationToken);

        _logger.LogInformation(
            "LogDiarioActualizado | Usuario={Usuario} | Fecha={Fecha} | Procesados={Procesados} | NoProcesados={NoProcesados}",
            contexto.Usuario,
            contexto.Fecha,
            procesados,
            noProcesados);
    }

    public async Task<(int Procesados, int NoProcesados)> LeerContadoresAsync(
        string rutaLog,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(rutaLog))
            return (0, 0);

        var lineas = await File.ReadAllLinesAsync(rutaLog, cancellationToken);
        var procesados = 0;
        var noProcesados = 0;

        foreach (var linea in lineas)
        {
            var matchProcesados = RegexProcesados.Match(linea);
            if (matchProcesados.Success)
            {
                procesados = int.Parse(matchProcesados.Groups[1].Value);
                continue;
            }

            var matchNoProcesados = RegexNoProcesados.Match(linea);
            if (matchNoProcesados.Success)
                noProcesados = int.Parse(matchNoProcesados.Groups[1].Value);
        }

        return (procesados, noProcesados);
    }
}
