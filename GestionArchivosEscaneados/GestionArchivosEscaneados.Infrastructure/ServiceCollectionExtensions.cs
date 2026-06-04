using GestionArchivosEscaneados.Infrastructure.Api;
using GestionArchivosEscaneados.Infrastructure.Auth;
using GestionArchivosEscaneados.Infrastructure.Logging;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GestionArchivosEscaneados.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGestionArchivosInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RutasSettings>(configuration.GetSection("Rutas"));
        services.Configure<ApiCredentialsSettings>(configuration.GetSection("ApiCredentials"));

        services.AddHttpClient<SoporteApiService>();
        services.AddHttpClient<SoporteFisicoApiService>();

        services.AddSingleton<UncStorageService>();
        services.AddSingleton<LogDiarioService>();
        services.AddSingleton<UsuarioAuthService>();
        services.AddSingleton<SoporteProcesamientoService>();

        return services;
    }
}
