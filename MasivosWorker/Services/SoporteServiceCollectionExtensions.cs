using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Models.Dto;

namespace Services;

/// <summary>
/// Registro DI compartido para Worker 2 (MasivosWorker) y portal MVC.
/// Garantiza las mismas clases HTTP y la misma configuración ApiCredentials.
/// </summary>
public static class SoporteServiceCollectionExtensions
{
    public static IServiceCollection AddSoporteHelpharmaIntegracion(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<ApiCredentialsSettings>(
            configuration.GetSection("ApiCredentials"));

        services.AddHttpClient<SoporteApiService>();
        services.AddHttpClient<SoporteFisicoApiService>();
        services.AddSingleton<SoporteProcesamientoService>();

        return services;
    }
}
