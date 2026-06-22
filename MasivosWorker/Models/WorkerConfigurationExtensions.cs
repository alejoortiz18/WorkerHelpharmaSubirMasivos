using Microsoft.Extensions.Configuration;

namespace Models;

public static class WorkerConfigurationExtensions
{
    public static IConfigurationBuilder AddMasivosWorkerLocalOverrides(
        this IConfigurationBuilder configurationBuilder,
        string? environmentName)
    {
        configurationBuilder.AddJsonFile(
            "appsettings.local.json",
            optional: true,
            reloadOnChange: false);

        if (!string.IsNullOrWhiteSpace(environmentName))
        {
            configurationBuilder.AddJsonFile(
                $"appsettings.{environmentName}.local.json",
                optional: true,
                reloadOnChange: false);
        }

        configurationBuilder.AddEnvironmentVariables();
        return configurationBuilder;
    }
}
