using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;
using Services;

namespace Tests.Infrastructure;

/// <summary>
/// Carga la misma configuración que MasivosWorker y construye servicios reales para pruebas tipo producción.
/// </summary>
public static class Worker2IntegracionHelper
{
    private static readonly Lazy<IConfiguration> Configuracion = new(CargarConfiguracion);
    private static bool _licenciaInicializada;

    public static IConfiguration Config => Configuracion.Value;

    public static string RutaDocumentosTest =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".github", "DocumentosTest"));

    /// <summary>Raíz UNC de producción: env MASIVOS_UNC_RAIZ o Rutas:RaizUnc del appsettings.</summary>
    public static string RaizUncProduccion
    {
        get
        {
            var unc = Environment.GetEnvironmentVariable("MASIVOS_UNC_RAIZ");
            if (!string.IsNullOrWhiteSpace(unc))
                return unc.TrimEnd('\\', '/');

            return (Config.GetSection("Rutas").Get<RutasSettings>() ?? new RutasSettings())
                .RaizUnc
                .TrimEnd('\\', '/');
        }
    }

    public static bool UncProduccionDisponible =>
        !string.IsNullOrWhiteSpace(RaizUncProduccion) &&
        RaizUncProduccion.StartsWith(@"\\") &&
        Directory.Exists(RaizUncProduccion);

    public static string UsuarioPrueba =>
        Environment.GetEnvironmentVariable("MASIVOS_USUARIO_PRUEBA")?.Trim()
        ?? "alejandro.ortiz";

    public static string FechaPrueba =>
        Environment.GetEnvironmentVariable("MASIVOS_FECHA_PRUEBA")?.Trim()
        ?? DateTime.Now.ToString("yyyy-MM-dd");

    public static string NormalizarRutaUnc(string ruta) =>
        ruta.Replace('/', '\\').TrimEnd('\\');

    public static Worker2EscenarioProduccion CrearEscenarioProduccion() =>
        new(RaizUncProduccion, UsuarioPrueba, FechaPrueba);

    public static bool UsarApisReales =>
        string.Equals(
            Environment.GetEnvironmentVariable("MASIVOS_E2E_API"),
            "1",
            StringComparison.OrdinalIgnoreCase);

    public static bool UsarOpenAiReal =>
        string.Equals(
            Environment.GetEnvironmentVariable("MASIVOS_E2E_OPENAI"),
            "1",
            StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(Config["OpenAi:ApiKey"]);

    public static bool UsarEmailReal
    {
        get
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("MASIVOS_E2E_EMAIL"),
                    "1",
                    StringComparison.OrdinalIgnoreCase))
                return false;

            var email = Config.GetSection("Email").Get<EmailSettings>() ?? new EmailSettings();
            return email.Habilitado &&
                   !string.IsNullOrWhiteSpace(email.Usuario) &&
                   !string.IsNullOrWhiteSpace(email.Clave);
        }
    }

    public static EmailNotificationService CrearEmailReal() =>
        new(
            Options.Create(Config.GetSection("Email").Get<EmailSettings>() ?? new EmailSettings()),
            NullLogger<EmailNotificationService>.Instance);

    public static IOpenAiBarcodeService CrearOpenAiServicio() => CrearOpenAiReal();

    public static void InicializarLicenciaIronBarcode()
    {
        if (_licenciaInicializada)
            return;

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.Configure<IronBarcodeSettings>(Config.GetSection("IronBarcode"));
        services.AddSingleton<IronBarcodeLicenseInitializer>();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IronBarcodeLicenseInitializer>();
        _licenciaInicializada = true;
    }

    public static LoteProcesamientoService CrearServicioLote(
        ISoporteProcesamientoService? soporteOverride = null,
        IOpenAiBarcodeService? openAiOverride = null,
        IEmailNotificationService? emailOverride = null)
    {
        InicializarLicenciaIronBarcode();

        var fileSettings = Options.Create(
            Config.GetSection("FileSettings").Get<FileSettings>() ?? new FileSettings());

        var barcode = new BarcodeRegionService(NullLogger<BarcodeRegionService>.Instance);
        var soporte = soporteOverride ?? CrearSoporteReal();
        var documento = new DocumentoProcesamientoService(
            barcode,
            soporte,
            fileSettings,
            NullLogger<DocumentoProcesamientoService>.Instance);

        var fileManager = new FileManagerInfraestructure(
            fileSettings,
            NullLogger<FileManagerInfraestructure>.Instance);

        var logDiario = new LogDiarioService(NullLogger<LogDiarioService>.Instance);
        var openAi = openAiOverride ?? CrearOpenAiReal();
        var email = emailOverride ?? new EmailNotificationService(
            Options.Create(Config.GetSection("Email").Get<EmailSettings>() ?? new EmailSettings()),
            NullLogger<EmailNotificationService>.Instance);

        return new LoteProcesamientoService(
            fileManager,
            documento,
            openAi,
            email,
            logDiario,
            fileSettings,
            NullLogger<LoteProcesamientoService>.Instance);
    }

    private static ISoporteProcesamientoService CrearSoporteReal()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddSoporteHelpharmaIntegracion(Config);

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<ISoporteProcesamientoService>();
    }

    private static IOpenAiBarcodeService CrearOpenAiReal()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.Configure<OpenAiSettings>(Config.GetSection("OpenAi"));
        services.AddHttpClient<OpenAiBarcodeService>();
        services.AddSingleton<IOpenAiBarcodeService>(sp =>
            sp.GetRequiredService<OpenAiBarcodeService>());

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IOpenAiBarcodeService>();
    }

    public static string CopiarPdfPrueba(string nombrePdf, string carpetaProcesar)
    {
        var origen = Path.Combine(RutaDocumentosTest, nombrePdf);
        if (!File.Exists(origen))
            throw new FileNotFoundException($"PDF de prueba no encontrado: {origen}");

        Directory.CreateDirectory(carpetaProcesar);
        var destino = Path.Combine(carpetaProcesar, nombrePdf);
        File.Copy(origen, destino, overwrite: true);
        return destino;
    }

    public static string CopiarPdfPrueba(
        string nombrePdf,
        string carpetaProcesar,
        Worker2EscenarioProduccion escenario)
    {
        var destino = CopiarPdfPrueba(nombrePdf, carpetaProcesar);
        escenario.RegistrarArchivoDePrueba(destino);
        return destino;
    }

    private static IConfiguration CargarConfiguracion()
    {
        var rutaAppsettings = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(rutaAppsettings))
        {
            rutaAppsettings = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "MasivosWorker", "appsettings.json"));
        }

        return new ConfigurationBuilder()
            .AddJsonFile(rutaAppsettings, optional: false)
            .AddEnvironmentVariables()
            .Build();
    }
}
