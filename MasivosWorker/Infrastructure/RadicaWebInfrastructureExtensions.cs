using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Models.Dto;
using Services;

namespace Infrastructure;

public static class RadicaWebInfrastructureExtensions
{
    public static IServiceCollection AddRadicaWebInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RadicaWebSettings>(configuration.GetSection("RadicaWeb"));
        services.AddHttpClient<RadicaWebApiService>();
        services.AddSingleton<RadicaWebIntegracionService>();
        services.AddSingleton<IRadicaWebIntegracionService>(sp =>
            sp.GetRequiredService<RadicaWebIntegracionService>());

        return services;
    }
}
