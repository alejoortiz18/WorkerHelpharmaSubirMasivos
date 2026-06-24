using FluentAssertions;
using GestionArchivosEscaneados.Infrastructure;
using GestionArchivosEscaneados.Infrastructure.Api;
using GestionArchivosEscaneados.Infrastructure.Barcode;
using GestionArchivosEscaneados.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Tests.Infrastructure;

/// <summary>
/// Valida lectura OpenAI del texto bajo el código de barras (no del nombre del archivo).
/// Ejecutar: dotnet test --filter "FullyQualifiedName~OpenAiBarcodeProduccionE2ETests"
/// </summary>
[Trait("Category", "E2E")]
public class OpenAiBarcodeProduccionE2ETests
{
    private const string Usuario = "dgutierrez";
    private const string Fecha = "2026-06-24";
    private const string NombreArchivo = "CRC_900277244_FE256521.pdf";

    private const string PdfDgutierrez =
        @"\\192.168.0.69\ArchivosScaneados\dgutierrez\2026-06-24\noprocesados\CRC_900277244_FE256521.pdf";

    private const string CodigoEsperado = "FMI61068";

    /// <summary>Fragmentos del nombre del archivo que OpenAI no debe devolver como soporte.</summary>
    private static readonly string[] CodigosProhibidosDesdeNombre =
    [
        "FE256521",
        "256521",
        "900277244",
        "9002772444",
        "CRC"
    ];

    private static string ConfigBasePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "GestionArchivosEscaneados.Web"));

    private static string PromptCanónicoPath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..", "..",
        "MasivosWorker", "MasivosWorker", "Prompts", "barcode-openai.txt"));

    [Fact]
    public async Task LeerCodigoAsync_Dgutierrez_FE256521_UsaPromptYTextoBajoBarcode()
    {
        File.Exists(PdfDgutierrez).Should().BeTrue($"PDF no encontrado: {PdfDgutierrez}");
        File.Exists(PromptCanónicoPath).Should().BeTrue($"Prompt no encontrado: {PromptCanónicoPath}");

        var promptEsperado = File.ReadAllText(PromptCanónicoPath, System.Text.Encoding.UTF8).TrimEnd();
        promptEsperado.Should().Contain("IGNORA el nombre del archivo PDF");

        var trace = new List<string>();
        await using var provider = CrearProveedor(trace);

        var openAi = provider.GetRequiredService<IOpenAiBarcodeService>();
        var soporteApi = provider.GetRequiredService<SoporteApiService>();

        var resultado = await openAi.LeerCodigoAsync(PdfDgutierrez);

        Console.WriteLine($"=== OpenAI E2E | Usuario={Usuario} | Fecha={Fecha} | Archivo={NombreArchivo} ===");
        Console.WriteLine(string.Join(Environment.NewLine, trace));
        Console.WriteLine($"Respuesta cruda OpenAI: '{resultado.RespuestaCruda ?? "-"}'");
        Console.WriteLine($"Codigo interpretado: '{resultado.Codigo ?? "-"}' | Tipo={resultado.Tipo}");

        trace.Should().Contain(l =>
            l.Contains("OpenAiPromptCargadoDeArchivo", StringComparison.Ordinal));
        trace.Should().Contain(l =>
            l.Contains("OpenAiRespuestaCruda", StringComparison.Ordinal),
            "Debe registrarse el texto crudo devuelto por OpenAI.");

        resultado.RespuestaCruda.Should().NotBeNullOrWhiteSpace(
            "OpenAI debe devolver el texto leído del PDF.");

        foreach (var prohibido in CodigosProhibidosDesdeNombre)
        {
            resultado.RespuestaCruda!.ToUpperInvariant().Should().NotContain(
                prohibido,
                $"la respuesta no debe copiar '{prohibido}' del nombre del archivo {NombreArchivo}");
        }

        resultado.Tipo.Should().Be(OpenAiBarcodeResultKind.CodigoEncontrado,
            "OpenAI debe encontrar un código con letras+números bajo el barcode del documento.");

        foreach (var prohibido in CodigosProhibidosDesdeNombre)
        {
            resultado.Codigo!.Should().NotBe(
                prohibido,
                "el código interpretado no debe coincidir con fragmentos del nombre del archivo");
        }

        resultado.RespuestaCruda.Should().BeOneOf(CodigoEsperado, "FM161068",
            "OpenAI debe leer FMI61068; FM161068 es aceptable si la API corrige I↔1.");
        resultado.Codigo.Should().BeOneOf(CodigoEsperado, "FM161068");

        var datosSoporte = await soporteApi.EnviarSoporteAsync(CodigoEsperado);
        datosSoporte.Should().NotBeNull(
            $"DatosSoportes debe responder para el soporte {CodigoEsperado}.");
        Console.WriteLine($"DatosSoportes OK | Paciente={datosSoporte!.NombrePaciente}");
        datosSoporte.NombrePaciente.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task LeerCodigoAsync_Dgutierrez_FE256832_ReportaTextoCrudo()
    {
        const string nombreArchivo = "CRC_900277244_FE256832.pdf";
        const string pdf =
            @"\\192.168.0.69\ArchivosScaneados\dgutierrez\2026-06-24\noprocesados\CRC_900277244_FE256832.pdf";

        File.Exists(pdf).Should().BeTrue($"PDF no encontrado: {pdf}");

        var trace = new List<string>();
        await using var provider = CrearProveedor(trace);

        var openAi = provider.GetRequiredService<IOpenAiBarcodeService>();
        var resultado = await openAi.LeerCodigoAsync(pdf);

        Console.WriteLine($"=== OpenAI E2E | Usuario={Usuario} | Fecha={Fecha} | Archivo={nombreArchivo} ===");
        Console.WriteLine(string.Join(Environment.NewLine, trace));
        Console.WriteLine($"Respuesta cruda OpenAI: '{resultado.RespuestaCruda ?? "-"}'");
        Console.WriteLine($"Codigo interpretado: '{resultado.Codigo ?? "-"}' | Tipo={resultado.Tipo}");

        resultado.RespuestaCruda.Should().NotBeNullOrWhiteSpace();
    }

    private static ServiceProvider CrearProveedor(List<string> trace)
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
            builder.AddProvider(new SimpleTraceLoggerProvider(trace));
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddGestionArchivosInfrastructure(configRoot);
        return services.BuildServiceProvider();
    }

    private sealed class SimpleTraceLoggerProvider(List<string> trace) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new SimpleTraceLogger(trace, categoryName);
        public void Dispose() { }
    }

    private sealed class SimpleTraceLogger(List<string> trace, string category) : ILogger
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
            if (!string.IsNullOrWhiteSpace(msg)
                && (msg.Contains("OpenAi", StringComparison.Ordinal)
                    || category.Contains("OpenAiBarcodeService", StringComparison.Ordinal)))
            {
                trace.Add(msg);
            }
        }
    }
}
