using System.Text.RegularExpressions;
using GestionArchivosEscaneados.Models.Entities;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Infrastructure.Logging;

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

    public async Task<ResumenLogDiario> LeerResumenAsync(
        RutasDiaContext rutas,
        CancellationToken cancellationToken = default)
    {
        var (procesados, noProcesados) = await LeerContadoresAsync(rutas.RutaLogDiario, cancellationToken);
        return new ResumenLogDiario
        {
            CantidadProcesados = procesados,
            NoProcesados = noProcesados
        };
    }

    public async Task IncrementarProcesadosAsync(
        RutasDiaContext rutas,
        int delta = 1,
        CancellationToken cancellationToken = default)
    {
        await IncrementarAsync(rutas, delta, 0, cancellationToken);
    }

    public async Task RegistrarReprocesoExitosoAsync(
        RutasDiaContext rutas,
        CancellationToken cancellationToken = default)
    {
        await IncrementarAsync(rutas, 1, -1, cancellationToken);
    }

    public Task RegistrarEliminacionAsync(
        RutasDiaContext rutas,
        CancellationToken cancellationToken = default) =>
        IncrementarAsync(rutas, 0, -1, cancellationToken);

    public async Task IncrementarAsync(
        RutasDiaContext rutas,
        int procesadosDelta,
        int noProcesadosDelta,
        CancellationToken cancellationToken = default)
    {
        if (procesadosDelta == 0 && noProcesadosDelta == 0)
            return;

        Directory.CreateDirectory(rutas.Log);
        var (procesados, noProcesados) = await LeerContadoresAsync(rutas.RutaLogDiario, cancellationToken);
        procesados += procesadosDelta;
        noProcesados += noProcesadosDelta;
        if (noProcesados < 0)
            noProcesados = 0;

        await File.WriteAllTextAsync(
            rutas.RutaLogDiario,
            $"CantidadProcesados:{procesados}{Environment.NewLine}NoProcesados:{noProcesados}{Environment.NewLine}",
            cancellationToken);

        _logger.LogInformation(
            "LogDiarioActualizado | Usuario={Usuario} | Fecha={Fecha} | Procesados={Procesados} | NoProcesados={NoProcesados}",
            rutas.Usuario,
            rutas.Fecha,
            procesados,
            noProcesados);
    }

    private static async Task<(int Procesados, int NoProcesados)> LeerContadoresAsync(
        string rutaLog,
        CancellationToken cancellationToken)
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
