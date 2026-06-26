using FluentAssertions;
using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Infrastructure;
using GestionArchivosEscaneados.Infrastructure.Api;
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
/// Verifica que Procesar documentos llame POST DatosSoportes con { "soporte": "..." }.
/// Ejecutar: dotnet test --filter "FullyQualifiedName~ArchivoPdfDatosSoportesTests"
/// </summary>
[Trait("Category", "Funcional")]
public class ArchivoPdfDatosSoportesTests
{
    private const string PdfOrigen = @"C:\Users\serviciosrelease\Documents\Desarrollos\workerHelpharmaSubirArchivos\WorkerHelpharmaSubirMasivos\ArchivosTest\archivo.pdf";
    private const string Archivo = "archivo.pdf";
    private const string Usuario = "dgutierrez";
    private const string Fecha = "2026-06-25";
    private const string Soporte = "FMI58590";

    private static string ConfigBasePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "GestionArchivosEscaneados.Web"));

    [Fact]
    public async Task EnviarSoporteAsync_FMI58590_LlamaDatosSoportesConBodySoporte()
    {
        File.Exists(PdfOrigen).Should().BeTrue($"Debe existir el PDF de prueba: {PdfOrigen}");

        var trace = new TraceLogCollector(capturarTodo: true);
        await using var provider = CrearProveedor(trace);

        var productos = provider.GetRequiredService<IConfiguracionProductoService>();
        var trazabilidad = provider.GetRequiredService<ITrazabilidadConsultaSqlService>();
        var config = provider.GetRequiredService<IIntegracionConfigProvider>();
        var soporteApi = provider.GetRequiredService<SoporteApiService>();

        await trazabilidad.EnsureSchemaAsync();
        await productos.SembrarDesdeAppSettingsSiFaltanAsync();

        var endpoint = await config.ObtenerSoporteApiUrlAsync();
        var apiKey = await config.ObtenerSoporteApiKeyAsync();

        endpoint.Should().Contain("DatosSoportes");
        apiKey.Should().Be("ABC123456789");

        var respuesta = await soporteApi.EnviarSoporteAsync(Soporte);

        Console.WriteLine(string.Join(Environment.NewLine, trace.Lines));
        respuesta.Should().NotBeNull($"La API DatosSoportes debe responder para soporte {Soporte}. Logs:\n{trace.Text}");
        respuesta!.NombrePaciente.Should().NotBeNullOrWhiteSpace();
        respuesta.IdPaciente.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProcesarConCodigoConocidoAsync_ArchivoPdf_FMI58590_CicloProcesarDocumentos()
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

        PrepararArchivoPendiente(uncConexion, unc);
        await RegistrarPendienteEnBdAsync(configuration);

        var pendientes = await reproceso.ListarNoProcesadosAsync(Usuario, Fecha);
        pendientes.Should().Contain(p => p.NombreArchivo.Equals(Archivo, StringComparison.OrdinalIgnoreCase));

        var rutaPdf = unc.ResolverRutaPdfSegura(Usuario, Fecha, Archivo)!;
        File.Exists(rutaPdf).Should().BeTrue();

        trace.Lines.Add($"=== Procesar documentos simulado | Soporte={Soporte} | Archivo={Archivo} ===");

        var estado = await reproceso.ProcesarConCodigoConocidoAsync(Usuario, Fecha, Archivo, Soporte);

        Console.WriteLine(string.Join(Environment.NewLine, trace.Lines));
        Console.WriteLine($"Estado final: {estado}");

        trace.Text.Should().Contain("DatosSoportes");
        trace.Text.Should().NotContain("ApiSoporteError", "DatosSoportes debe responder OK con FMI58590.");

        var tamanoMb = new FileInfo(PdfOrigen).Length / (1024.0 * 1024.0);
        trace.Lines.Add($"Tamano PDF: {tamanoMb:F2} MB (limite API fisico: 20 MB)");

        if (tamanoMb > 20)
        {
            estado.Should().Be(
                SoporteProcesamientoEstado.FalloApiFisico,
                "Tras DatosSoportes OK, un PDF >20 MB debe fallar en soporte fisico.");
            trace.Text.Should().Contain("SoporteFisicoError");
            File.Exists(rutaPdf).Should().BeTrue("El PDF grande permanece en noprocesados.");
            return;
        }

        estado.Should().Be(SoporteProcesamientoEstado.Exito);
        trace.Text.Should().Contain("SoporteProcesamientoOK");
        File.Exists(rutaPdf).Should().BeFalse();
        var rutas = unc.ObtenerRutasDia(Usuario, Fecha);
        File.Exists(Path.Combine(rutas.Procesados, Archivo)).Should().BeTrue();
    }

    private static void PrepararArchivoPendiente(UncConexionService uncConexion, UncStorageService unc)
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

            File.Copy(PdfOrigen, destino, overwrite: true);
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
                || msg.Contains("Soporte", StringComparison.Ordinal)
                || msg.Contains("ApiSoporte", StringComparison.Ordinal)
                || msg.Contains("ProcesoDocumento", StringComparison.Ordinal))
            {
                collector.Lines.Add(exception != null ? $"{msg} | Ex={exception.Message}" : msg);
            }
        }
    }
}
