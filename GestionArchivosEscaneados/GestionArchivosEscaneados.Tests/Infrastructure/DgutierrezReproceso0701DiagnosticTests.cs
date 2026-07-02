using FluentAssertions;
using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Infrastructure;
using GestionArchivosEscaneados.Infrastructure.Api;
using GestionArchivosEscaneados.Infrastructure.Barcode;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Tests.Infrastructure;

[Trait("Category", "E2E")]
public class DgutierrezReproceso0701DiagnosticTests
{
    private const string Inetpub = @"C:\inetpub\GestionDocumentosEscaneados";
    private const string Usuario = "dgutierrez";
    private const string Fecha = "2026-07-01";

    [Fact]
    public async Task Diagnostic_LeerBarcodeYOpenAi_FE264633()
    {
        const string pdf = @"\\192.168.0.69\ArchivosScaneados\dgutierrez\2026-07-01\noprocesados\CRC_900277244_FE264633.pdf";
        File.Exists(pdf).Should().BeTrue();

        await using var provider = CrearProveedorProduccion(new TraceLogCollector(capturarTodo: true));
        var barcode = provider.GetRequiredService<IBarcodeRegionService>();
        var openAi = provider.GetRequiredService<IOpenAiBarcodeService>();
        var soporteApi = provider.GetRequiredService<SoporteApiService>();

        var codigoBarcode = barcode.LeerCodigoDesdePdf(pdf);
        Console.WriteLine($"IronBarcode: {codigoBarcode ?? "(null)"}");

        var openAiResult = await openAi.LeerCodigoAsync(pdf);
        Console.WriteLine($"OpenAI crudo: {openAiResult.RespuestaCruda} | Codigo: {openAiResult.Codigo} | Tipo: {openAiResult.Tipo}");

        foreach (var candidato in new[] { codigoBarcode, openAiResult.Codigo, "IM401565", "IM402284", "FMI401565", "FMI264633" }.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var variante in SoporteCodigoOcrHelper.VariantesConsultaDatosSoportes(candidato!))
            {
                var respuesta = await soporteApi.EnviarSoporteAsync(variante);
                Console.WriteLine($"API {variante}: {(respuesta == null ? "null" : respuesta.NombrePaciente ?? "OK")}");
                if (respuesta != null)
                    return;
            }
        }

        throw new InvalidOperationException("Ningun candidato respondio DatosSoportes.");
    }

    [Fact]
    public async Task ReprocesarAsync_Dgutierrez_20260701_Diagnostic()
    {
        var trace = new TraceLogCollector(capturarTodo: true);
        await using var provider = CrearProveedorProduccion(trace);

        var reproceso = provider.GetRequiredService<ReprocesoAppService>();
        var unc = provider.GetRequiredService<UncStorageService>();

        var pendientes = await reproceso.ListarNoProcesadosAsync(Usuario, Fecha);
        trace.Lines.Add($"Pendientes visibles UI: {pendientes.Count}");
        foreach (var p in pendientes)
            trace.Lines.Add($"  - {p.NombreArchivo} | intentoPrevio={p.TieneIntentoPrevio}");

        pendientes.Should().NotBeEmpty();

        foreach (var objetivo in pendientes)
        {
            var rutaPdf = unc.ResolverRutaPdfSegura(Usuario, Fecha, objetivo.NombreArchivo);
            trace.Lines.Add($"=== Reprocesando {objetivo.NombreArchivo} ===");
            trace.Lines.Add($"PDF: {rutaPdf}");
            trace.Lines.Add($"Existe PDF: {rutaPdf != null && File.Exists(rutaPdf)}");

            var estado = await reproceso.ReprocesarAsync(
                Usuario,
                Fecha,
                objetivo.NombreArchivo,
                string.Empty);

            trace.Lines.Add($"Estado: {estado}");
            if (objetivo.NombreArchivo.Contains("FE265075", StringComparison.OrdinalIgnoreCase))
            {
                estado.Should().Be(SoporteProcesamientoEstado.Exito, trace.Text);
            }
            else
            {
                trace.Lines.Add($"Resultado esperado variable para {objetivo.NombreArchivo}");
            }
        }

        var salida = string.Join(Environment.NewLine, trace.Lines);
        Console.WriteLine(salida);
        Console.WriteLine("--- Trace ---");
        Console.WriteLine(trace.Text);

        trace.Text.Should().Contain("ReprocesoInicio");
    }

    private static ServiceProvider CrearProveedorProduccion(TraceLogCollector trace)
    {
        var configBuilder = new ConfigurationBuilder()
            .SetBasePath(Inetpub)
            .AddJsonFile("appsettings.json", optional: false);

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
