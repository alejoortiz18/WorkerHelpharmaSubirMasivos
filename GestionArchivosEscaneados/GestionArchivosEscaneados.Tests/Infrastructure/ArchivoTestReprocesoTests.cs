using FluentAssertions;
using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Infrastructure;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Enums;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Tests.Infrastructure;

/// <summary>
/// Reproceso funcional con PDF de ArchivosTest.
/// Ejecutar: dotnet test --filter "FullyQualifiedName~ArchivoTestReprocesoTests"
/// </summary>
[Trait("Category", "Funcional")]
public class ArchivoTestReprocesoTests
{
    private const string PdfOrigen = @"C:\Users\serviciosrelease\Documents\Desarrollos\workerHelpharmaSubirArchivos\WorkerHelpharmaSubirMasivos\ArchivosTest\CRC_900277244_FE249758.pdf";
    private const string Archivo = "CRC_900277244_FE249758.pdf";
    private const string Usuario = "dgutierrez";
    private const string Fecha = "2026-06-25";

    private static string ConfigBasePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "GestionArchivosEscaneados.Web"));

    [Fact]
    public async Task ReprocesarAsync_ArchivoTest_FE249758_CicloCompletoConConfigBd()
    {
        File.Exists(PdfOrigen).Should().BeTrue($"Debe existir el PDF de prueba: {PdfOrigen}");

        var trace = new TraceLogCollector(capturarTodo: true);
        await using var provider = CrearProveedor(trace);

        var trazabilidad = provider.GetRequiredService<ITrazabilidadConsultaSqlService>();
        var productos = provider.GetRequiredService<IConfiguracionProductoService>();
        var config = provider.GetRequiredService<IIntegracionConfigProvider>();
        var uncConexion = provider.GetRequiredService<UncConexionService>();
        var reproceso = provider.GetRequiredService<ReprocesoAppService>();
        var unc = provider.GetRequiredService<UncStorageService>();
        var configuration = provider.GetRequiredService<IConfiguration>();

        await trazabilidad.EnsureSchemaAsync();
        await productos.SembrarDesdeAppSettingsSiFaltanAsync();

        var raizUnc = (await config.ObtenerRaizUncAsync()).Trim();
        uncConexion.AsegurarAccesoUnc().Should().BeTrue($"UNC accesible: {raizUnc}");

        PrepararArchivoPendiente(uncConexion, unc, PdfOrigen);
        await RegistrarPendienteEnBdAsync(configuration);

        var pendientes = await reproceso.ListarNoProcesadosAsync(Usuario, Fecha);
        pendientes.Should().Contain(p => p.NombreArchivo.Equals(Archivo, StringComparison.OrdinalIgnoreCase));

        var rutaPdf = unc.ResolverRutaPdfSegura(Usuario, Fecha, Archivo)!;
        File.Exists(rutaPdf).Should().BeTrue();

        var rutas = unc.ObtenerRutasDia(Usuario, Fecha);
        var rutaProcesados = Path.Combine(rutas.Procesados, Archivo);
        var rutaAttempt = rutaPdf + ".attempt";
        if (File.Exists(rutaAttempt))
            File.Delete(rutaAttempt);

        trace.Lines.Add($"=== ArchivoTest FE249758 | Usuario={Usuario} | Fecha={Fecha} ===");
        trace.Lines.Add($"Origen: {PdfOrigen}");
        trace.Lines.Add($"Destino noprocesados: {rutaPdf}");

        var estado = await reproceso.ReprocesarAsync(Usuario, Fecha, Archivo, string.Empty);

        trace.Lines.Add($"Estado final: {estado}");
        Console.WriteLine(string.Join(Environment.NewLine, trace.Lines));
        Console.WriteLine("--- Trazabilidad ---");
        Console.WriteLine(trace.Text);

        trace.Text.Should().Contain("ReprocesoInicio");
        File.Exists(rutaAttempt).Should().BeTrue();

        var usoBarcode = trace.Text.Contains("ReprocesoBarcodeDetectado", StringComparison.Ordinal);
        var usoOpenAi = trace.Text.Contains("ReprocesoOpenAiResultado", StringComparison.Ordinal);
        (usoBarcode || usoOpenAi).Should().BeTrue("Debe intentar IronBarCode u OpenAI.");

        if (estado == SoporteProcesamientoEstado.Exito)
        {
            trace.Text.Should().Contain("ReprocesoExitoso");
            trace.Text.Should().Contain("SoporteProcesamientoOK");
            trace.Text.Should().Contain("SoporteFisicoOK");
            File.Exists(rutaPdf).Should().BeFalse();
            File.Exists(rutaProcesados).Should().BeTrue();

            var procesados = await trazabilidad.ListarDocumentosProcesadosAsync(Usuario, Fecha);
            procesados.Should().Contain(d =>
                d.NombreArchivo.Equals(Archivo, StringComparison.OrdinalIgnoreCase)
                && d.Procesado
                && !string.IsNullOrWhiteSpace(d.Soporte));
        }
        else
        {
            trace.Text.Should().MatchRegex(
                "Reproceso(SoporteFallo|BarcodeNoDetectado|OpenAiResultado|EnviarSoporte)",
                $"Ciclo ejecutado pero terminó en {estado}.\n{trace.Text}");
            File.Exists(rutaPdf).Should().BeTrue("Si falla, el PDF permanece en noprocesados.");
        }
    }

    private static void PrepararArchivoPendiente(UncConexionService uncConexion, UncStorageService unc, string pdfOrigen)
    {
        uncConexion.EjecutarConAcceso(() =>
        {
            var rutas = unc.ObtenerRutasDia(Usuario, Fecha);
            Directory.CreateDirectory(rutas.Noprocesados);
            Directory.CreateDirectory(rutas.Procesados);

            var destino = Path.Combine(rutas.Noprocesados, Archivo);
            var enProcesados = Path.Combine(rutas.Procesados, Archivo);
            if (File.Exists(enProcesados))
                File.Delete(enProcesados);

            File.Copy(pdfOrigen, destino, overwrite: true);
        });
    }

    private static async Task RegistrarPendienteEnBdAsync(IConfiguration configuration)
    {
        var connectionString = configuration["TrazabilidadSql:ConnectionString"]
            ?? throw new InvalidOperationException("TrazabilidadSql:ConnectionString no configurada.");

        const string sql = """
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRAN;

DECLARE @UsuarioId int;
DECLARE @FechaId int;

SELECT @UsuarioId = UsuarioId
FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK)
WHERE NombreUsuario = @NombreUsuario;

IF @UsuarioId IS NULL
BEGIN
    INSERT INTO dbo.Usuarios (NombreUsuario) VALUES (@NombreUsuario);
    SET @UsuarioId = SCOPE_IDENTITY();
END

SELECT @FechaId = FechaProcesamientoId
FROM dbo.FechasProcesamiento WITH (UPDLOCK, HOLDLOCK)
WHERE UsuarioId = @UsuarioId AND FechaProcesamiento = @FechaProcesamiento;

IF @FechaId IS NULL
BEGIN
    INSERT INTO dbo.FechasProcesamiento (UsuarioId, FechaProcesamiento)
    VALUES (@UsuarioId, @FechaProcesamiento);
    SET @FechaId = SCOPE_IDENTITY();
END

UPDATE dbo.DocumentosProcesados
SET Procesado = 0, Soporte = NULL, IdPaciente = NULL, IdBodega = NULL, IdCartera = NULL, FechaFactura = NULL
WHERE FechaProcesamientoId = @FechaId AND NombreArchivo = @NombreArchivo;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO dbo.DocumentosProcesados (FechaProcesamientoId, NombreArchivo, Procesado)
    VALUES (@FechaId, @NombreArchivo, 0);
END

COMMIT TRAN;
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@NombreUsuario", Usuario);
        command.Parameters.AddWithValue("@NombreArchivo", Archivo);
        command.Parameters.Add("@FechaProcesamiento", System.Data.SqlDbType.Date).Value =
            DateTime.ParseExact(Fecha, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        await command.ExecuteNonQueryAsync();
    }

    private static ServiceProvider CrearProveedor(TraceLogCollector trace)
    {
        var credenciales = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "scripts", "appsettings.Production.local.json"));

        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(ConfigBasePath)
            .AddJsonFile("appsettings.json", optional: false);

        if (File.Exists(credenciales))
            configBuilder.AddJsonFile(credenciales, optional: false);

        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Rutas:RaizUnc"] = "\\\\SENTINEL\\NO_USAR",
            ["Red:Usuario"] = "sentinel-user",
            ["Red:Clave"] = "sentinel-clave",
            ["ApiCredentials:SoporteApiKey"] = "SENTINEL-API-KEY-INVALIDA",
            ["ApiCredentials:SoporteFisicoToken"] = "SENTINEL-TOKEN-INVALIDO",
            ["OpenAi:ApiKey"] = "sk-SENTINEL-NO-USAR"
        });

        var configRoot = configBuilder.Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configRoot);
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(new TraceLoggerProvider(trace));
            builder.SetMinimumLevel(LogLevel.Information);
        });
        services.AddGestionArchivosInfrastructure(configRoot);
        services.AddGestionArchivosApplication();

        return services.BuildServiceProvider();
    }

    private sealed class TraceLogCollector(bool capturarTodo = false)
    {
        public List<string> Lines { get; } = [];

        public string Text => string.Join('\n', Lines);

        public bool CapturarTodo => capturarTodo;
    }

    private sealed class TraceLoggerProvider(TraceLogCollector collector) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new TraceLogger(collector);

        public void Dispose() { }
    }

    private sealed class TraceLogger(TraceLogCollector collector) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var msg = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(msg))
                return;

            if (collector.CapturarTodo
                || msg.Contains("Reproceso", StringComparison.Ordinal)
                || msg.Contains("OpenAi", StringComparison.Ordinal)
                || msg.Contains("Soporte", StringComparison.Ordinal)
                || msg.Contains("Barcode", StringComparison.Ordinal)
                || msg.Contains("LeyendoPdf", StringComparison.Ordinal)
                || msg.Contains("ApiSoporte", StringComparison.Ordinal))
            {
                collector.Lines.Add(exception != null ? $"{msg} | Ex={exception.Message}" : msg);
            }
        }
    }
}
