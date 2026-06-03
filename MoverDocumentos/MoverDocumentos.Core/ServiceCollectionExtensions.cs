using Microsoft.Extensions.DependencyInjection;
using MoverDocumentos.Core.Configuration;
using MoverDocumentos.Core.Services;

namespace MoverDocumentos.Core;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMoverDocumentosCore(this IServiceCollection services)
    {
        services.AddSingleton<UsuarioService>();
        services.AddSingleton<RegistroUsuarioService>();
        services.AddSingleton<EstructuraCarpetasService>();
        services.AddSingleton<RedDisponibleService>();
        services.AddSingleton<MoverArchivoService>();
        services.AddSingleton<LoteService>();
        services.AddHostedService<EscaneoWatcherService>();

        return services;
    }
}
