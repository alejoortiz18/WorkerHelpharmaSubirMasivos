using FluentAssertions;
using GestionArchivosEscaneados.Infrastructure;
using GestionArchivosEscaneados.Infrastructure.Api;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GestionArchivosEscaneados.Tests.Infrastructure;

[Trait("Category", "Funcional")]
public class Ke485416DatosSoportesTests
{
    private static string ConfigBasePath => @"C:\inetpub\GestionDocumentosEscaneados";

    [Fact]
    public async Task EnviarSoporteAsync_KE485416_DeserializaRespuesta()
    {
        if (!File.Exists(Path.Combine(ConfigBasePath, "appsettings.json")))
        {
            return;
        }

        var config = new ConfigurationBuilder()
            .SetBasePath(ConfigBasePath)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddGestionArchivosInfrastructure(config);
        await using var provider = services.BuildServiceProvider();

        var trazabilidad = provider.GetRequiredService<ITrazabilidadConsultaSqlService>();
        var productos = provider.GetRequiredService<IConfiguracionProductoService>();
        await trazabilidad.EnsureSchemaAsync();
        await productos.SembrarDesdeAppSettingsSiFaltanAsync();

        var soporteApi = provider.GetRequiredService<SoporteApiService>();
        foreach (var codigo in new[] { "KE-485416", "KE485416" })
        {
            var respuesta = await soporteApi.EnviarSoporteAsync(codigo);
            respuesta.Should().NotBeNull($"DatosSoportes debe responder para {codigo}");
            respuesta!.NombrePaciente.Should().NotBeNullOrWhiteSpace();
            respuesta.IdPaciente.Should().Be("26052410168721");
        }
    }
}
