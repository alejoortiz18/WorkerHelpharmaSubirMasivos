using FluentAssertions;
using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Infrastructure;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Tests.Infrastructure;

/// <summary>
/// Validación funcional pre-despliegue: configuración desde BD y flujo Documentos no procesados.
/// Ejecutar: dotnet test --filter "FullyQualifiedName~ConfiguracionBdFuncionalTests"
/// </summary>
[Trait("Category", "Funcional")]
public class ConfiguracionBdFuncionalTests
{
    private static string ConfigBasePath => Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..", "..", "..", "..",
        "GestionArchivosEscaneados.Web"));

    [Fact]
    public async Task Configuraciones_TodasLasIntegracionesExistenEnBd()
    {
        await using var provider = CrearProveedorConAppsettingsAlterados();
        var productos = provider.GetRequiredService<IConfiguracionProductoService>();
        var trazabilidad = provider.GetRequiredService<ITrazabilidadConsultaSqlService>();

        await trazabilidad.EnsureSchemaAsync();
        await productos.SembrarDesdeAppSettingsSiFaltanAsync();

        var filas = await productos.ListarAsync();
        filas.Select(f => f.Producto).Should().BeEquivalentTo(ProductoIntegracion.Todos);
    }

    [Fact]
    public async Task IntegracionConfigProvider_PriorizaBdSobreAppsettings()
    {
        await using var provider = CrearProveedorConAppsettingsAlterados();
        var trazabilidad = provider.GetRequiredService<ITrazabilidadConsultaSqlService>();
        var productos = provider.GetRequiredService<IConfiguracionProductoService>();
        var config = provider.GetRequiredService<IIntegracionConfigProvider>();

        await trazabilidad.EnsureSchemaAsync();
        await productos.SembrarDesdeAppSettingsSiFaltanAsync();

        var filas = (await productos.ListarAsync()).ToDictionary(f => f.Producto, StringComparer.Ordinal);

        filas.Should().ContainKey(ProductoIntegracion.Unc);
        filas.Should().ContainKey(ProductoIntegracion.SoporteApi);
        filas.Should().ContainKey(ProductoIntegracion.SoporteFisico);
        filas.Should().ContainKey(ProductoIntegracion.OpenAi);
        filas.Should().ContainKey(ProductoIntegracion.RadicaWeb);

        (await config.ObtenerRaizUncAsync()).Should().Be(filas[ProductoIntegracion.Unc].Endpoint!.Trim());
        (await config.ObtenerRedUsuarioAsync()).Should().Be(filas[ProductoIntegracion.Unc].ClaveCredencial!.Trim());
        (await config.ObtenerRedClaveAsync()).Should().Be(filas[ProductoIntegracion.Unc].ValorAdicional!.Trim());

        (await config.ObtenerSoporteApiUrlAsync()).Should().Be(filas[ProductoIntegracion.SoporteApi].Endpoint!.Trim());
        (await config.ObtenerSoporteApiKeyAsync()).Should().Be(filas[ProductoIntegracion.SoporteApi].ClaveCredencial!.Trim());

        (await config.ObtenerSoporteFisicoApiUrlAsync()).Should().Be(filas[ProductoIntegracion.SoporteFisico].Endpoint!.Trim());
        (await config.ObtenerSoporteFisicoTokenAsync()).Should().Be(filas[ProductoIntegracion.SoporteFisico].ClaveCredencial!.Trim());
        (await config.ObtenerIdUsuarioSoporteFisicoAsync()).Should().Be(filas[ProductoIntegracion.SoporteFisico].ValorAdicional!.Trim());

        (await config.ObtenerOpenAiApiUrlAsync()).Should().Be(filas[ProductoIntegracion.OpenAi].Endpoint!.Trim());
        (await config.ObtenerOpenAiModelsUrlAsync()).Should().Be(filas[ProductoIntegracion.OpenAi].EndpointVerificacion!.Trim());
        (await config.ObtenerOpenAiApiKeyAsync()).Should().Be(filas[ProductoIntegracion.OpenAi].ClaveCredencial!.Trim());
        (await config.ObtenerOpenAiModelAsync()).Should().Be(filas[ProductoIntegracion.OpenAi].ValorAdicional!.Trim());

        (await config.ObtenerRadicaWebApiUrlAsync()).Should().Be(filas[ProductoIntegracion.RadicaWeb].Endpoint!.Trim());
        (await config.ObtenerRadicaWebApiClientAsync()).Should().Be(filas[ProductoIntegracion.RadicaWeb].ClaveCredencial!.Trim());
        (await config.ObtenerRadicaWebApiSecretAsync()).Should().Be(filas[ProductoIntegracion.RadicaWeb].ValorAdicional!.Trim());

        var promptBd = filas[ProductoIntegracion.OpenAi].Prompt;
        if (!string.IsNullOrWhiteSpace(promptBd))
            (await config.ObtenerOpenAiPromptAsync()).Should().Be(promptBd.Trim());

        // appsettings tiene valores centinela distintos; si la app leyera appsettings, fallaría.
        (await config.ObtenerRaizUncAsync()).Should().NotBe("\\\\SENTINEL\\NO_USAR");
        (await config.ObtenerSoporteApiKeyAsync()).Should().NotBe("SENTINEL-API-KEY-INVALIDA");
        (await config.ObtenerOpenAiApiKeyAsync()).Should().NotBe("sk-SENTINEL-NO-USAR");
    }

    [Fact]
    public async Task UncConexionService_UsaRaizUncDeBd()
    {
        await using var provider = CrearProveedorConAppsettingsAlterados();
        var trazabilidad = provider.GetRequiredService<ITrazabilidadConsultaSqlService>();
        var productos = provider.GetRequiredService<IConfiguracionProductoService>();
        var config = provider.GetRequiredService<IIntegracionConfigProvider>();
        var unc = provider.GetRequiredService<UncConexionService>();

        await trazabilidad.EnsureSchemaAsync();
        await productos.SembrarDesdeAppSettingsSiFaltanAsync();

        var raizBd = (await config.ObtenerRaizUncAsync()).Trim();
        raizBd.Should().NotBeNullOrWhiteSpace();
        raizBd.Should().NotBe("\\\\SENTINEL\\NO_USAR");

        var accesible = unc.AsegurarAccesoUnc();
        accesible.Should().BeTrue($"La ruta UNC de BD debe ser accesible: {raizBd}. Error: {unc.UltimoErrorMensaje}");
    }

    [Fact]
    public async Task DocumentosNoProcesados_ListarYValidarPdfEnUnc()
    {
        const string usuario = "dgutierrez";
        const string fecha = "2026-06-25";

        await using var provider = CrearProveedorConAppsettingsAlterados();
        var trazabilidad = provider.GetRequiredService<ITrazabilidadConsultaSqlService>();
        var productos = provider.GetRequiredService<IConfiguracionProductoService>();
        var reproceso = provider.GetRequiredService<ReprocesoAppService>();
        var unc = provider.GetRequiredService<UncStorageService>();

        await trazabilidad.EnsureSchemaAsync();
        await productos.SembrarDesdeAppSettingsSiFaltanAsync();

        var pendientes = await reproceso.ListarNoProcesadosAsync(usuario, fecha);
        pendientes.Should().NotBeEmpty($"Debe haber documentos no procesados para {usuario} / {fecha}.");

        foreach (var doc in pendientes.Take(3))
        {
            var ruta = unc.ResolverRutaPdfSegura(usuario, fecha, doc.NombreArchivo);
            ruta.Should().NotBeNullOrWhiteSpace($"PDF esperado en UNC: {doc.NombreArchivo}");
            File.Exists(ruta!).Should().BeTrue($"El PDF debe existir físicamente: {ruta}");
        }
    }

    [Fact]
    public async Task ProcesarConCodigoConocidoAsync_UsaApisConConfigBd()
    {
        const string usuario = "dgutierrez";
        const string fecha = "2026-06-25";
        const string archivo = "CRC_900277244_FE258978.pdf";
        const string codigo = "FMI61068";

        await using var provider = CrearProveedorConAppsettingsAlterados();
        var trazabilidad = provider.GetRequiredService<ITrazabilidadConsultaSqlService>();
        var productos = provider.GetRequiredService<IConfiguracionProductoService>();
        var reproceso = provider.GetRequiredService<ReprocesoAppService>();
        var unc = provider.GetRequiredService<UncStorageService>();

        await trazabilidad.EnsureSchemaAsync();
        await productos.SembrarDesdeAppSettingsSiFaltanAsync();

        var pendientes = await reproceso.ListarNoProcesadosAsync(usuario, fecha);
        var siguePendiente = pendientes.Any(p =>
            p.NombreArchivo.Equals(archivo, StringComparison.OrdinalIgnoreCase));

        if (!siguePendiente)
        {
            var procesados = await trazabilidad.ListarDocumentosProcesadosAsync(usuario, fecha);
            var yaProcesado = procesados.Any(d =>
                d.NombreArchivo.Equals(archivo, StringComparison.OrdinalIgnoreCase)
                && string.Equals(d.Soporte, codigo, StringComparison.OrdinalIgnoreCase));

            yaProcesado.Should().BeTrue(
                "El documento debe estar procesado en BD tras una ejecución exitosa previa del flujo Procesar lote.");
            return;
        }

        var rutaPdf = unc.ResolverRutaPdfSegura(usuario, fecha, archivo)!;
        File.Exists(rutaPdf).Should().BeTrue();

        var estado = await reproceso.ProcesarConCodigoConocidoAsync(usuario, fecha, archivo, codigo);
        estado.Should().Be(SoporteProcesamientoEstado.Exito);

        File.Exists(rutaPdf).Should().BeFalse();
        var rutas = unc.ObtenerRutasDia(usuario, fecha);
        File.Exists(Path.Combine(rutas.Procesados, archivo)).Should().BeTrue();
    }

    [Fact]
    public async Task ReprocesarAsync_UnDocumentoPendiente_CicloCompletoConConfigBd()
    {
        const string usuario = "dgutierrez";
        const string fecha = "2026-06-25";

        var trace = new TraceLogCollector(capturarTodo: true);
        await using var provider = CrearProveedorConAppsettingsAlterados(trace);

        var trazabilidad = provider.GetRequiredService<ITrazabilidadConsultaSqlService>();
        var productos = provider.GetRequiredService<IConfiguracionProductoService>();
        var reproceso = provider.GetRequiredService<ReprocesoAppService>();
        var unc = provider.GetRequiredService<UncStorageService>();

        await trazabilidad.EnsureSchemaAsync();
        await productos.SembrarDesdeAppSettingsSiFaltanAsync();

        var pendientes = await reproceso.ListarNoProcesadosAsync(usuario, fecha);
        pendientes.Should().NotBeEmpty();

        var objetivo = pendientes.First(p =>
        {
            var ruta = unc.ResolverRutaPdfSegura(usuario, fecha, p.NombreArchivo);
            return ruta != null && File.Exists(ruta);
        });

        var rutaPdf = unc.ResolverRutaPdfSegura(usuario, fecha, objetivo.NombreArchivo)!;
        var rutas = unc.ObtenerRutasDia(usuario, fecha);
        var rutaProcesados = Path.Combine(rutas.Procesados, objetivo.NombreArchivo);
        var rutaAttempt = rutaPdf + ".attempt";

        trace.Lines.Add($"=== Funcional reproceso | Usuario={usuario} | Fecha={fecha} | Archivo={objetivo.NombreArchivo} ===");
        trace.Lines.Add($"PDF: {rutaPdf}");

        var estado = await reproceso.ReprocesarAsync(usuario, fecha, objetivo.NombreArchivo, string.Empty);

        trace.Lines.Add($"Estado: {estado}");
        Console.WriteLine(string.Join(Environment.NewLine, trace.Lines));
        Console.WriteLine("--- Trazabilidad ---");
        Console.WriteLine(trace.Text);

        trace.Text.Should().Contain("ReprocesoInicio");
        File.Exists(rutaAttempt).Should().BeTrue("Debe crear marcador .attempt");

        var usoBarcode = trace.Text.Contains("ReprocesoBarcodeDetectado", StringComparison.Ordinal);
        var usoOpenAi = trace.Text.Contains("ReprocesoOpenAiResultado", StringComparison.Ordinal);
        (usoBarcode || usoOpenAi).Should().BeTrue("Debe intentar lectura barcode u OpenAI.");

        if (estado == SoporteProcesamientoEstado.Exito)
        {
            trace.Text.Should().Contain("ReprocesoExitoso");
            trace.Text.Should().Contain("SoporteProcesamientoOK");
            File.Exists(rutaPdf).Should().BeFalse("PDF exitoso sale de noprocesados");
            File.Exists(rutaProcesados).Should().BeTrue("PDF exitoso va a procesados");
        }
        else
        {
            trace.Text.Should().MatchRegex(
                "Reproceso(SoporteFallo|BarcodeNoDetectado|OpenAiResultado)",
                "Debe quedar trazabilidad del fallo.");
            File.Exists(rutaPdf).Should().BeTrue("Si falla, el PDF permanece en noprocesados");
        }
    }

    private static ServiceProvider CrearProveedorConAppsettingsAlterados(TraceLogCollector? trace = null)
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

        // Valores centinela: si la app los usara, las aserciones fallarían.
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Rutas:RaizUnc"] = "\\\\SENTINEL\\NO_USAR",
            ["Red:Usuario"] = "sentinel-user",
            ["Red:Clave"] = "sentinel-clave",
            ["ApiCredentials:SoporteApiKey"] = "SENTINEL-API-KEY-INVALIDA",
            ["ApiCredentials:SoporteFisicoToken"] = "SENTINEL-TOKEN-INVALIDO",
            ["ApiCredentials:IdUsuario"] = "sentinel-id",
            ["OpenAi:ApiKey"] = "sk-SENTINEL-NO-USAR",
            ["OpenAi:Model"] = "sentinel-model"
        });

        var configRoot = configBuilder.Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configRoot);

        if (trace != null)
        {
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddProvider(new TraceLoggerProvider(trace));
                builder.SetMinimumLevel(LogLevel.Information);
            });
        }
        else
        {
            services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        }

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
                || msg.Contains("Barcode", StringComparison.Ordinal))
            {
                collector.Lines.Add(exception != null ? $"{msg} | Ex={exception.Message}" : msg);
            }
        }
    }
}
