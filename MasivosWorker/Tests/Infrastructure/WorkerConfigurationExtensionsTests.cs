using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Models;
using Xunit;

namespace Tests.Infrastructure;

public sealed class WorkerConfigurationExtensionsTests : IDisposable
{
    private readonly string _tempDirectory;

    public WorkerConfigurationExtensionsTests()
    {
        _tempDirectory = Path.Combine(
            Path.GetTempPath(),
            "worker-config-tests-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void AddMasivosWorkerLocalOverrides_AplicaOverrideEspecificoDeEntorno()
    {
        File.WriteAllText(
            Path.Combine(_tempDirectory, "appsettings.json"),
            """
            {
              "OpenAi": {
                "ApiKey": "base-key"
              },
              "Email": {
                "SmtpHost": "smtp.base.local"
              }
            }
            """);

        File.WriteAllText(
            Path.Combine(_tempDirectory, "appsettings.local.json"),
            """
            {
              "OpenAi": {
                "ApiKey": "local-key"
              },
              "Email": {
                "SmtpHost": "smtp.local"
              }
            }
            """);

        File.WriteAllText(
            Path.Combine(_tempDirectory, "appsettings.Production.local.json"),
            """
            {
              "OpenAi": {
                "ApiKey": "prod-local-key"
              },
              "Email": {
                "SmtpHost": "smtp.prod.local"
              }
            }
            """);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(_tempDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddMasivosWorkerLocalOverrides("Production")
            .Build();

        configuration["OpenAi:ApiKey"].Should().Be("prod-local-key");
        configuration["Email:SmtpHost"].Should().Be("smtp.prod.local");
    }

    [Fact]
    public void AddMasivosWorkerLocalOverrides_NoFallaSiNoHayArchivosLocales()
    {
        File.WriteAllText(
            Path.Combine(_tempDirectory, "appsettings.json"),
            """
            {
              "OpenAi": {
                "ApiKey": "base-key"
              }
            }
            """);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(_tempDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .AddMasivosWorkerLocalOverrides("Production")
            .Build();

        configuration["OpenAi:ApiKey"].Should().Be("base-key");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }
}
