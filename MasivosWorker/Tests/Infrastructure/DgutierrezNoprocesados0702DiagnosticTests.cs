using FluentAssertions;
using Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
using Models.Dto;
using Services;
using Xunit;

namespace Tests.Infrastructure;

[Trait("Category", "E2E")]
public class DgutierrezNoprocesados0702DiagnosticTests
{
    private const string ServiciosWorker = @"C:\Servicios\MasivosWorker";
    private const string Usuario = "dgutierrez";
    private const string Fecha = "2026-07-02";
    private const string CarpetaNoprocesados =
        @"\\192.168.0.69\ArchivosScaneados\dgutierrez\2026-07-02\noprocesados";

    [Fact]
    public async Task Diagnostic_ProcesarPdfsNoprocesados_0702()
    {
        Directory.Exists(CarpetaNoprocesados).Should().BeTrue("La carpeta UNC debe estar accesible");

        var pdfs = Directory.GetFiles(CarpetaNoprocesados, "*.pdf")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        pdfs.Should().NotBeEmpty();

        await using var provider = CrearProveedorProduccion(out var config);
        var documentoService = provider.GetRequiredService<DocumentoProcesamientoService>();
        var barcode = provider.GetRequiredService<BarcodeRegionService>();
        var openAi = provider.GetRequiredService<IOpenAiBarcodeService>();
        var soporteApi = provider.GetRequiredService<SoporteApiService>();

        var lineas = new List<string>();
        var exitos = 0;
        var fallosApi = 0;
        var fallosBarcode = 0;

        foreach (var pdf in pdfs)
        {
            var nombre = Path.GetFileName(pdf);
            lineas.Add($"=== {nombre} ===");

            var resumenBd = await LeerResumenTrazabilidadAsync(config, Usuario, Fecha, nombre);
            lineas.Add($"BD: Soporte={resumenBd.Soporte ?? "(null)"} Procesado={resumenBd.Procesado}");

            var codigoBarcode = barcode.LeerCodigoDesdePdf(pdf);
            lineas.Add($"IronBarcode: {codigoBarcode ?? "(null)"}");

            if (string.Equals(Environment.GetEnvironmentVariable("MASIVOS_E2E_OPENAI"), "1", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(config["OpenAi:ApiKey"]))
            {
                var openAiResult = await openAi.LeerCodigoAsync(pdf);
                lineas.Add($"OpenAI: {openAiResult.Codigo ?? "(null)"} | Tipo={openAiResult.Tipo}");
            }

            if (!string.IsNullOrWhiteSpace(resumenBd.Soporte))
            {
                var apiDirecto = await soporteApi.EnviarSoporteAsync(resumenBd.Soporte);
                lineas.Add($"API directo ({resumenBd.Soporte}): {(apiDirecto == null ? "null" : apiDirecto.NombrePaciente ?? "OK")}");

                if (apiDirecto == null)
                {
                    foreach (var variante in SoporteCodigoOcrHelper.VariantesConsultaDatosSoportes(resumenBd.Soporte))
                    {
                        var resp = await soporteApi.EnviarSoporteAsync(variante);
                        if (resp == null)
                            continue;

                        lineas.Add($"API variante OK: {variante} -> {resp.NombrePaciente ?? "OK"}");
                        break;
                    }
                }
            }

            if (!string.Equals(Environment.GetEnvironmentVariable("MASIVOS_E2E_API"), "1", StringComparison.OrdinalIgnoreCase))
            {
                lineas.Add("Omitiendo ProcesarAsync (MASIVOS_E2E_API!=1)");
                continue;
            }

            DocumentoProcesamientoResult resultado;
            if (!string.IsNullOrWhiteSpace(resumenBd.Soporte))
            {
                resultado = await documentoService.ProcesarConCodigoConocidoAsync(pdf, resumenBd.Soporte);
            }
            else if (!string.IsNullOrWhiteSpace(codigoBarcode))
            {
                resultado = await documentoService.ProcesarConCodigoConocidoAsync(pdf, codigoBarcode);
            }
            else
            {
                resultado = await documentoService.ProcesarAsync(pdf);
            }

            lineas.Add($"Procesar: Estado={resultado.Estado} Soporte={resultado.Soporte ?? "(null)"} IdPaciente={resultado.IdPaciente?.ToString() ?? "(null)"}");

            switch (resultado.Estado)
            {
                case DocumentoProcesamientoEstado.Exito:
                    exitos++;
                    break;
                case DocumentoProcesamientoEstado.FalloApiDatos:
                case DocumentoProcesamientoEstado.FalloApiFisico:
                    fallosApi++;
                    break;
                case DocumentoProcesamientoEstado.FalloBarcode:
                    fallosBarcode++;
                    break;
            }
        }

        var salida = string.Join(Environment.NewLine, lineas);
        Console.WriteLine(salida);
        Console.WriteLine($"Resumen: exitos={exitos} fallosApi={fallosApi} fallosBarcode={fallosBarcode} total={pdfs.Count}");

        pdfs.Count.Should().BeGreaterThan(0);
    }

    private static ServiceProvider CrearProveedorProduccion(out IConfiguration config)
    {
        config = new ConfigurationBuilder()
            .SetBasePath(ServiciosWorker)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Production.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        Worker2IntegracionHelper.InicializarLicenciaIronBarcode();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Information));
        services.AddSingleton<IConfiguration>(config);
        services.Configure<FileSettings>(config.GetSection("FileSettings"));
        services.Configure<IronBarcodeSettings>(config.GetSection("IronBarcode"));
        services.AddSoporteHelpharmaIntegracion(config);
        services.Configure<OpenAiSettings>(config.GetSection("OpenAi"));
        services.AddHttpClient<OpenAiBarcodeService>();
        services.AddSingleton<IOpenAiBarcodeService>(sp => sp.GetRequiredService<OpenAiBarcodeService>());
        services.AddSingleton<BarcodeRegionService>();
        services.AddSingleton<DocumentoProcesamientoService>();

        return services.BuildServiceProvider();
    }

    private static async Task<(string? Soporte, bool Procesado)> LeerResumenTrazabilidadAsync(
        IConfiguration config,
        string usuario,
        string fecha,
        string nombreArchivo)
    {
        const string sql = """
SELECT TOP 1 dp.Soporte, dp.Procesado
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @Usuario
  AND fp.FechaProcesamiento = @Fecha
  AND dp.NombreArchivo = @NombreArchivo;
""";

        var connectionString = new SqlConnectionStringBuilder(
            config.GetSection("TrazabilidadSql").Get<TrazabilidadSqlSettings>()?.ConnectionString ?? string.Empty)
        {
            InitialCatalog = "Scaneados"
        }.ConnectionString;

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Usuario", usuario);
        command.Parameters.Add("@Fecha", System.Data.SqlDbType.Date).Value =
            DateOnly.ParseExact(fecha, "yyyy-MM-dd").ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@NombreArchivo", nombreArchivo);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return (null, false);

        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            !reader.IsDBNull(1) && reader.GetBoolean(1));
    }
}
