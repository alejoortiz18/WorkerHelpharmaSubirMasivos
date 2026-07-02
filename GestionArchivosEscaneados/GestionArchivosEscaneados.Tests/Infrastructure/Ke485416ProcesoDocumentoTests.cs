using FluentAssertions;
using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Infrastructure;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Tests.Infrastructure;

[Trait("Category", "Funcional")]
public class Ke485416ProcesoDocumentoTests
{
    private const string Usuario = "usuariopik";
    private const string Fecha = "2026-06-19";
    private const string Archivo = "CRC_900277244_650.pdf";
    private const string Soporte = "KE-485416";
    private static string ConfigBasePath => @"C:\inetpub\GestionDocumentosEscaneados";

    [Fact]
    public async Task ProcesarConCodigoConocidoAsync_KE485416_Usuariopik_20260619()
    {
        if (!File.Exists(Path.Combine(ConfigBasePath, "appsettings.json")))
            return;

        var config = new ConfigurationBuilder()
            .SetBasePath(ConfigBasePath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddGestionArchivosInfrastructure(config);
        services.AddGestionArchivosApplication();
        await using var provider = services.BuildServiceProvider();

        var trazabilidad = provider.GetRequiredService<ITrazabilidadConsultaSqlService>();
        var productos = provider.GetRequiredService<IConfiguracionProductoService>();
        var uncConexion = provider.GetRequiredService<UncConexionService>();
        var reproceso = provider.GetRequiredService<ReprocesoAppService>();
        var unc = provider.GetRequiredService<UncStorageService>();

        await trazabilidad.EnsureSchemaAsync();
        await productos.SembrarDesdeAppSettingsSiFaltanAsync();
        uncConexion.AsegurarAccesoUnc().Should().BeTrue();

        var pendientes = await reproceso.ListarNoProcesadosAsync(Usuario, Fecha);
        pendientes.Should().Contain(p => p.NombreArchivo.Equals(Archivo, StringComparison.OrdinalIgnoreCase));

        var rutaPdf = unc.ResolverRutaPdfSegura(Usuario, Fecha, Archivo);
        File.Exists(rutaPdf!).Should().BeTrue($"PDF pendiente: {rutaPdf}");

        var estado = await reproceso.ProcesarConCodigoConocidoAsync(Usuario, Fecha, Archivo, Soporte);
        estado.Should().Be(SoporteProcesamientoEstado.Exito, "DatosSoportes debe deserializar idPaciente largo y completar el flujo");

        File.Exists(rutaPdf!).Should().BeFalse("El PDF debe moverse a procesados");
        var rutas = unc.ObtenerRutasDia(Usuario, Fecha);
        File.Exists(Path.Combine(rutas.Procesados, Archivo)).Should().BeTrue();
    }
}
