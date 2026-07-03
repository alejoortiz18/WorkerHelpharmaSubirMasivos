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
using Xunit.Abstractions;

namespace Tests.Infrastructure;

/// <summary>
/// E2E completo sobre lote real en ArchivosNuevos (sin desplegar servicio).
/// Detener MasivosWorker antes de ejecutar para evitar carrera con el servicio instalado.
///
/// dotnet test --filter "FullyQualifiedName~UsuariopikLoteRadicaWebE2ETest" --logger "console;verbosity=detailed"
/// </summary>
[Trait("Category", "E2E")]
public class UsuariopikLoteRadicaWebE2ETest
{
    private const string ServiciosWorker = @"C:\Servicios\MasivosWorker";
    private const string TxtLote =
        @"\\192.168.0.69\ArchivosScaneados\ArchivosNuevos\usuariopik-2026-07-03 08-52-00AM.txt";
    private const string Usuario = "usuariopik";
    private const string Fecha = "2026-07-03";

    private readonly ITestOutputHelper _output;

    public UsuariopikLoteRadicaWebE2ETest(ITestOutputHelper output) => _output = output;

    [Fact]
    public async Task ProcesarLoteReal_Completo_IncluyeRadicaWeb()
    {
        File.Exists(TxtLote).Should().BeTrue($"Debe existir el TXT del lote: {TxtLote}");

        var config = CargarConfiguracionProduccion();
        config.GetSection("RadicaWeb").Get<RadicaWebSettings>()!.EstaConfigurado.Should().BeTrue(
            "Configure RadicaWeb__ApiClient y RadicaWeb__ApiSecret antes de ejecutar");

        Worker2IntegracionHelper.InicializarLicenciaIronBarcode();

        await using var provider = CrearProveedor(config);
        await provider.GetRequiredService<ITrazabilidadSqlService>().EnsureSchemaAsync();

        var rutaProcesar = await LoteProcesamientoService.LeerRutaProcesarDesdeTxtAsync(TxtLote);
        var contexto = RutasLoteResolver.Resolver(rutaProcesar);

        var pdfsAntes = ContarPdfs(contexto.Procesar);
        var procesadosAntes = ContarPdfs(contexto.Procesados);
        var radicaWebAntes = await ContarRadicaWebAsync(config, Usuario);

        _output.WriteLine($"Antes | procesar={pdfsAntes} procesados={procesadosAntes} RadicaWebAPI={radicaWebAntes}");
        _output.WriteLine($"TXT={TxtLote}");
        _output.WriteLine($"RutaProcesar={rutaProcesar}");

        var servicio = provider.GetRequiredService<LoteProcesamientoService>();
        var inicio = DateTimeOffset.Now;

        var outcome = await servicio.ProcesarLoteAsync(TxtLote, CancellationToken.None);

        var duracion = DateTimeOffset.Now - inicio;

        _output.WriteLine($"Outcome={outcome.Estado} | Procesados={outcome.Procesados} | NoProcesados={outcome.NoProcesados} | Duracion={duracion}");

        var pdfsDespues = ContarPdfs(contexto.Procesar);
        var procesadosDespues = ContarPdfs(contexto.Procesados);
        var radicaWebDespues = await ContarRadicaWebAsync(config, Usuario);
        var radicaWebNuevos = radicaWebDespues - radicaWebAntes;

        _output.WriteLine($"Despues | procesar={pdfsDespues} procesados={procesadosDespues} RadicaWebAPI={radicaWebDespues} (+{radicaWebNuevos})");

        var ultimosRadicaWeb = await LeerUltimosRadicaWebAsync(config, Usuario, 10);
        foreach (var r in ultimosRadicaWeb)
            _output.WriteLine($"RadicaWeb | Fecha={r.FechaFactura:yyyy-MM-dd} Bodega={r.Bodega} Success={r.Success} Status={r.StatusCode} Msg={Truncar(r.Message, 80)}");

        outcome.Estado.Should().BeOneOf(LoteProcesamientoEstado.Completado, LoteProcesamientoEstado.PendienteReintento);

        if (outcome.Estado == LoteProcesamientoEstado.Completado)
            File.Exists(TxtLote).Should().BeFalse("el TXT debe eliminarse al completar el lote");

        (outcome.Procesados + procesadosAntes).Should().BeGreaterThan(procesadosAntes, "debe haber procesado al menos un PDF");

        if (procesadosDespues > 0)
            radicaWebNuevos.Should().BeGreaterThan(0, "debe registrar llamadas RadicaWeb cuando hay PDFs exitosos en el lote");
    }

    private static ServiceProvider CrearProveedor(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Information));
        services.AddSingleton(config);
        services.Configure<FileSettings>(config.GetSection("FileSettings"));
        services.Configure<IronBarcodeSettings>(config.GetSection("IronBarcode"));
        services.Configure<RutasSettings>(config.GetSection("Rutas"));
        services.Configure<RedSettings>(config.GetSection("Red"));
        services.Configure<TrazabilidadSqlSettings>(config.GetSection("TrazabilidadSql"));
        services.Configure<OpenAiSettings>(config.GetSection("OpenAi"));
        services.Configure<EmailSettings>(config.GetSection("Email"));
        services.AddSoporteHelpharmaIntegracion(config);
        services.AddRadicaWebInfrastructure(config);
        services.AddSingleton<RedDisponibleService>();
        services.AddSingleton<FileManagerInfraestructure>();
        services.AddSingleton<BarcodeRegionService>();
        services.AddSingleton<DocumentoProcesamientoService>();
        services.AddSingleton<IDocumentoProcesamientoService>(sp => sp.GetRequiredService<DocumentoProcesamientoService>());
        services.AddHttpClient<OpenAiBarcodeService>();
        services.AddSingleton<IOpenAiBarcodeService>(sp => sp.GetRequiredService<OpenAiBarcodeService>());
        services.AddSingleton<EmailNotificationService>();
        services.AddSingleton<IEmailNotificationService>(sp => sp.GetRequiredService<EmailNotificationService>());
        services.AddSingleton<TrazabilidadSqlService>();
        services.AddSingleton<ITrazabilidadSqlService>(sp => sp.GetRequiredService<TrazabilidadSqlService>());
        services.AddSingleton<LoteProcesamientoService>();
        services.AddSingleton<IronBarcodeLicenseInitializer>();

        var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IronBarcodeLicenseInitializer>();

        return provider;
    }

    private static IConfiguration CargarConfiguracionProduccion() =>
        new ConfigurationBuilder()
            .SetBasePath(ServiciosWorker)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Production.local.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

    private static int ContarPdfs(string carpeta) =>
        Directory.Exists(carpeta) ? Directory.GetFiles(carpeta, "*.pdf").Length : 0;

    private static async Task<int> ContarRadicaWebAsync(IConfiguration config, string usuario)
    {
        const string sql = """
SELECT COUNT(*)
FROM dbo.RadicaWebAPI rw
INNER JOIN dbo.Usuarios u ON u.UsuarioId = rw.UsuarioId
WHERE u.NombreUsuario = @Usuario;
""";
        return await EjecutarEscalarAsync(config, sql, usuario);
    }

    private static async Task<List<(DateOnly FechaFactura, string Bodega, bool? Success, int? StatusCode, string? Message)>> LeerUltimosRadicaWebAsync(
        IConfiguration config,
        string usuario,
        int top)
    {
        var sql = $"""
SELECT TOP ({top}) rw.FechaFactura, rw.Bodega, rw.Success, rw.StatusCode, rw.Message
FROM dbo.RadicaWebAPI rw
INNER JOIN dbo.Usuarios u ON u.UsuarioId = rw.UsuarioId
WHERE u.NombreUsuario = @Usuario
ORDER BY rw.CreadoEn DESC;
""";
        var lista = new List<(DateOnly, string, bool?, int?, string?)>();
        var cs = ConnectionString(config);

        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Usuario", usuario);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            lista.Add((
                DateOnly.FromDateTime(reader.GetDateTime(0)),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetBoolean(2),
                reader.IsDBNull(3) ? null : reader.GetInt32(3),
                reader.IsDBNull(4) ? null : reader.GetString(4)));
        }

        return lista;
    }

    private static async Task<int> EjecutarEscalarAsync(IConfiguration config, string sql, string usuario)
    {
        var cs = ConnectionString(config);
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddWithValue("@Usuario", usuario);
        var result = await cmd.ExecuteScalarAsync();
        return result is null or DBNull ? 0 : Convert.ToInt32(result);
    }

    private static string ConnectionString(IConfiguration config) =>
        new SqlConnectionStringBuilder(
            config.GetSection("TrazabilidadSql").Get<TrazabilidadSqlSettings>()?.ConnectionString ?? string.Empty)
        {
            InitialCatalog = "Scaneados"
        }.ConnectionString;

    private static string? Truncar(string? texto, int max) =>
        string.IsNullOrEmpty(texto) || texto.Length <= max ? texto : texto[..max] + "...";
}
