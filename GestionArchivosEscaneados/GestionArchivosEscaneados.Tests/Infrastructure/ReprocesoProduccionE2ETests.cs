using FluentAssertions;
using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Infrastructure;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Tests.Infrastructure;

/// <summary>
/// E2E real contra UNC, SQL, IronBarCode, OpenAI y APIs de soporte (misma config que IIS).
/// Ejecutar: dotnet test --filter "FullyQualifiedName~ReprocesoProduccionE2ETests"
/// </summary>
[Trait("Category", "E2E")]
public class ReprocesoProduccionE2ETests
{
    private static string ConfigBasePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "GestionArchivosEscaneados.Web"));

    private const string Inetpub = @"C:\inetpub\GestionDocumentosEscaneados";
    private const string Usuario = "dgutierrez";
    private const string Fecha = "2026-06-24";
    private const string ArchivoFe256521 = "CRC_900277244_FE256521.pdf";
    private const string CodigoResueltoApi = "FMI61068";

    [Fact]
    public async Task ReprocesarAsync_FE256521_CicloCompleto()
    {
        var trace = new TraceLogCollector(capturarTodo: true);
        await using var provider = CrearProveedorProduccion(trace);

        var reproceso = provider.GetRequiredService<ReprocesoAppService>();
        var unc = provider.GetRequiredService<UncStorageService>();

        var pendientes = await reproceso.ListarNoProcesadosAsync(Usuario, Fecha);
        pendientes.Should().Contain(
            p => p.NombreArchivo.Equals(ArchivoFe256521, StringComparison.OrdinalIgnoreCase),
            $"El archivo {ArchivoFe256521} debe estar pendiente en BD para {Usuario} / {Fecha}.");

        var rutaPdf = unc.ResolverRutaPdfSegura(Usuario, Fecha, ArchivoFe256521);
        rutaPdf.Should().NotBeNullOrWhiteSpace();
        File.Exists(rutaPdf!).Should().BeTrue("El PDF debe existir en noprocesados.");

        var rutas = unc.ObtenerRutasDia(Usuario, Fecha);
        var rutaProcesados = Path.Combine(rutas.Procesados, ArchivoFe256521);
        var rutaAttempt = rutaPdf + ".attempt";

        trace.Lines.Add($"=== E2E Ciclo completo | Usuario={Usuario} | Fecha={Fecha} | Archivo={ArchivoFe256521} ===");
        trace.Lines.Add($"PDF noprocesados: {rutaPdf}");
        trace.Lines.Add($"Destino procesados: {rutaProcesados}");
        trace.Lines.Add($"Attempt previo: {File.Exists(rutaAttempt)}");
        trace.Lines.Add($"En procesados antes: {File.Exists(rutaProcesados)}");

        var estado = await reproceso.ReprocesarAsync(
            Usuario,
            Fecha,
            ArchivoFe256521,
            string.Empty);

        trace.Lines.Add($"Estado final: {estado}");
        trace.Lines.Add($"Attempt despues: {File.Exists(rutaAttempt)}");
        trace.Lines.Add($"PDF en noprocesados despues: {File.Exists(rutaPdf!)}");
        trace.Lines.Add($"PDF en procesados despues: {File.Exists(rutaProcesados)}");

        var salida = string.Join(Environment.NewLine, trace.Lines);
        Console.WriteLine(salida);
        Console.WriteLine("--- Trazabilidad completa del ciclo ---");
        Console.WriteLine(trace.Text);

        trace.Text.Should().Contain("ReprocesoInicio");
        File.Exists(rutaAttempt).Should().BeTrue("Reprocesar debe crear o refrescar el marcador .attempt");

        var usoBarcode = trace.Text.Contains("ReprocesoBarcodeDetectado", StringComparison.Ordinal);
        var usoOpenAi = trace.Text.Contains("ReprocesoBarcodeNoDetectado", StringComparison.Ordinal)
            && trace.Text.Contains("ReprocesoOpenAiResultado", StringComparison.Ordinal);

        (usoBarcode || usoOpenAi).Should().BeTrue(
            "Debe ejecutarse lectura IronBarCode y, si falla, fallback OpenAI.\n" + trace.Text);

        if (usoOpenAi)
        {
            trace.Text.Should().Contain("ReprocesoOpenAiResultado");
            trace.Text.Should().Contain("OpenAiRespuestaCruda");
            trace.Text.Should().NotContain("Codigo=FE256521",
                "OpenAI no debe copiar FE256521 del nombre del archivo.");
        }

        trace.Text.Should().MatchRegex(
            $@"ReprocesoEnviarSoporte \| Archivo={ArchivoFe256521} \| Codigo=(FM161068|{CodigoResueltoApi})",
            "Debe enviar a APIs el código leído bajo el barcode.");

        trace.Text.Should().Contain("SoporteProcesamientoOK");
        trace.Text.Should().Contain("SoporteFisicoOK");
        trace.Text.Should().Contain("ReprocesoExitoso");

        estado.Should().Be(SoporteProcesamientoEstado.Exito,
            "El ciclo completo debe terminar en éxito.\n" + trace.Text);

        File.Exists(rutaPdf!).Should().BeFalse("PDF exitoso debe salir de noprocesados");
        File.Exists(rutaProcesados).Should().BeTrue("PDF exitoso debe estar en procesados");
    }

    [Fact]
    public async Task ReprocesarAsync_UnDocumento_UsaBarcodeOOpenAiSegunCorresponda()
    {
        var trace = new TraceLogCollector();
        await using var provider = CrearProveedorProduccion(trace);

        var reproceso = provider.GetRequiredService<ReprocesoAppService>();
        var unc = provider.GetRequiredService<UncStorageService>();

        var pendientes = await reproceso.ListarNoProcesadosAsync(Usuario, Fecha);
        pendientes.Should().NotBeEmpty($"No hay PDFs pendientes para {Usuario} en {Fecha}.");

        var objetivo = pendientes.FirstOrDefault(p =>
        {
            var ruta = unc.ResolverRutaPdfSegura(Usuario, Fecha, p.NombreArchivo);
            return ruta != null && !File.Exists(ruta + ".attempt");
        }) ?? pendientes[0];
        var rutaPdf = unc.ResolverRutaPdfSegura(Usuario, Fecha, objetivo.NombreArchivo);
        rutaPdf.Should().NotBeNullOrWhiteSpace();
        File.Exists(rutaPdf!).Should().BeTrue();

        var rutaAttempt = rutaPdf + ".attempt";
        var teniaAttempt = File.Exists(rutaAttempt);

        trace.Lines.Add($"=== E2E Reproceso | Usuario={Usuario} | Fecha={Fecha} | Archivo={objetivo.NombreArchivo} ===");
        trace.Lines.Add($"PDF: {rutaPdf}");
        trace.Lines.Add($"Attempt previo: {teniaAttempt}");

        var estado = await reproceso.ReprocesarAsync(
            Usuario,
            Fecha,
            objetivo.NombreArchivo,
            string.Empty);

        trace.Lines.Add($"Estado final: {estado}");

        var salida = string.Join(Environment.NewLine, trace.Lines);
        Console.WriteLine(salida);
        Console.WriteLine("--- Eventos capturados ---");
        Console.WriteLine(trace.Text);

        trace.Text.Should().Contain("ReprocesoInicio");

        var usoBarcode = trace.Text.Contains("ReprocesoBarcodeDetectado", StringComparison.Ordinal);
        var usoOpenAi = trace.Text.Contains("ReprocesoBarcodeNoDetectado", StringComparison.Ordinal)
            && (trace.Text.Contains("OpenAiResultado", StringComparison.Ordinal)
                || trace.Text.Contains("ReprocesoOpenAiResultado", StringComparison.Ordinal));

        (usoBarcode || usoOpenAi).Should().BeTrue(
            "Debe registrarse lectura por IronBarCode o fallback OpenAI.\n" + trace.Text);

        if (usoOpenAi)
            trace.Text.Should().Contain("ReprocesoOpenAiResultado");

        File.Exists(rutaAttempt).Should().BeTrue("Reprocesar debe crear el marcador .attempt");

        if (estado == SoporteProcesamientoEstado.Exito)
            File.Exists(rutaPdf!).Should().BeFalse("PDF exitoso debe salir de noprocesados");
    }

    private static ServiceProvider CrearProveedorProduccion(TraceLogCollector trace)
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
        public ILogger CreateLogger(string categoryName) => new TraceLogger(collector, categoryName);

        public void Dispose() { }
    }

    private sealed class TraceLogger(TraceLogCollector collector, string category) : ILogger
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
                || msg.Contains("LeyendoPdf", StringComparison.Ordinal)
                || msg.Contains("Soporte", StringComparison.Ordinal)
                || msg.Contains("ApiSoporte", StringComparison.Ordinal)
                || msg.Contains("Barcode", StringComparison.Ordinal)
                || msg.Contains("HTTP", StringComparison.Ordinal)
                || category.Contains("ReprocesoAppService", StringComparison.Ordinal)
                || category.Contains("OpenAiBarcodeService", StringComparison.Ordinal)
                || category.Contains("BarcodeRegionService", StringComparison.Ordinal)
                || category.Contains("SoporteProcesamientoService", StringComparison.Ordinal)
                || category.Contains("SoporteApiService", StringComparison.Ordinal)
                || category.Contains("SoporteFisicoApiService", StringComparison.Ordinal))
            {
                if (exception != null)
                    collector.Lines.Add($"{msg} | Ex={exception.Message}");
                else
                    collector.Lines.Add(msg);
            }
        }
    }
}
