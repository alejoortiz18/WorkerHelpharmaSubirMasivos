using GestionArchivosEscaneados.Infrastructure.Api;
using GestionArchivosEscaneados.Infrastructure.Barcode;
using GestionArchivosEscaneados.Infrastructure.Auth;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
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
        services.Configure<RedSettings>(configuration.GetSection("Red"));
        services.Configure<ApiCredentialsSettings>(configuration.GetSection("ApiCredentials"));
        services.Configure<FileSettings>(configuration.GetSection("FileSettings"));
        services.Configure<OpenAiSettings>(configuration.GetSection("OpenAi"));
        services.Configure<IronBarcodeSettings>(configuration.GetSection("IronBarcode"));
        services.Configure<TrazabilidadSqlSettings>(configuration.GetSection("TrazabilidadSql"));

        services.AddHttpClient<SoporteApiService>();
        services.AddHttpClient<SoporteFisicoApiService>();
        services.AddHttpClient<OpenAiBarcodeService>();

        services.AddSingleton<UncConexionService>();
        services.AddSingleton<UncStorageService>();
        services.AddSingleton<ITrazabilidadConsultaSqlService, TrazabilidadConsultaSqlService>();
        services.AddSingleton<UsuarioAuthService>();
        services.AddSingleton<IBarcodeRegionService, BarcodeRegionService>();
        services.AddSingleton<IOpenAiBarcodeService>(sp => sp.GetRequiredService<OpenAiBarcodeService>());
        services.AddSingleton<ISoporteProcesamientoService, SoporteProcesamientoService>();
        services.AddSingleton<IConfiguracionesService, ConfiguracionesService>();

        return services;
    }
}
