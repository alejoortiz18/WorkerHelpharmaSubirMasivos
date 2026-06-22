using System.Text.Json.Nodes;
using FluentAssertions;

namespace GestionArchivosEscaneados.Tests;

public class ConfigurationAlignmentTests
{
    [Fact]
    public void Portal_UsaMismoPromptModeloYCredencialesQueMasivosWorker()
    {
        var root = BuscarRaizRepositorio();

        var workerSettings = LeerJson(Path.Combine(root, "MasivosWorker", "MasivosWorker", "appsettings.json"));
        var portalSettings = LeerJson(Path.Combine(root, "GestionArchivosEscaneados", "GestionArchivosEscaneados.Web", "appsettings.json"));
        var workerCsproj = File.ReadAllText(Path.Combine(root, "MasivosWorker", "MasivosWorker", "MasivosWorker.csproj"));
        var portalCsproj = File.ReadAllText(Path.Combine(root, "GestionArchivosEscaneados", "GestionArchivosEscaneados.Web", "GestionArchivosEscaneados.Web.csproj"));
        var workerServicesCsproj = File.ReadAllText(Path.Combine(root, "MasivosWorker", "Services", "Services.csproj"));
        var portalInfrastructureCsproj = File.ReadAllText(Path.Combine(root, "GestionArchivosEscaneados", "GestionArchivosEscaneados.Infrastructure", "GestionArchivosEscaneados.Infrastructure.csproj"));

        portalSettings["OpenAi"]?["Model"]?.GetValue<string>()
            .Should().Be(workerSettings["OpenAi"]?["Model"]?.GetValue<string>());
        portalSettings["OpenAi"]?["PromptResourcePath"]?.GetValue<string>()
            .Should().Be(workerSettings["OpenAi"]?["PromptResourcePath"]?.GetValue<string>());
        portalSettings["ApiCredentials"]?["SoporteApiKey"]?.GetValue<string>()
            .Should().Be(workerSettings["ApiCredentials"]?["SoporteApiKey"]?.GetValue<string>());
        portalSettings["ApiCredentials"]?["SoporteFisicoToken"]?.GetValue<string>()
            .Should().Be(workerSettings["ApiCredentials"]?["SoporteFisicoToken"]?.GetValue<string>());
        portalSettings["ApiCredentials"]?["IdUsuario"]?.GetValue<string>()
            .Should().Be(workerSettings["ApiCredentials"]?["IdUsuario"]?.GetValue<string>());

        var workerPrompt = File.ReadAllText(Path.Combine(root, "MasivosWorker", "MasivosWorker", "Prompts", "barcode-openai.txt"));
        var portalPrompt = File.ReadAllText(Path.Combine(root, "GestionArchivosEscaneados", "GestionArchivosEscaneados.Web", "Prompts", "barcode-openai.txt"));

        portalPrompt.Should().Be(workerPrompt);
        portalCsproj.Should().Contain(ExtraerUserSecretsId(workerCsproj));
        ExtraerPackageVersion(portalInfrastructureCsproj, "BarCode")
            .Should().Be(ExtraerPackageVersion(workerServicesCsproj, "BarCode"));
        ExtraerPackageVersion(portalInfrastructureCsproj, "IronPdf")
            .Should().Be(ExtraerPackageVersion(workerServicesCsproj, "IronPdf"));
    }

    private static JsonNode LeerJson(string ruta)
    {
        var json = File.ReadAllText(ruta);
        return JsonNode.Parse(json) ?? throw new InvalidOperationException($"JSON invalido: {ruta}");
    }

    private static string BuscarRaizRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "MasivosWorker")) &&
                Directory.Exists(Path.Combine(dir.FullName, "GestionArchivosEscaneados")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("No se encontro la raiz del repositorio.");
    }

    private static string ExtraerUserSecretsId(string csproj)
    {
        const string inicio = "<UserSecretsId>";
        const string fin = "</UserSecretsId>";
        var indiceInicio = csproj.IndexOf(inicio, StringComparison.Ordinal);
        var indiceFin = csproj.IndexOf(fin, StringComparison.Ordinal);

        if (indiceInicio < 0 || indiceFin < 0 || indiceFin <= indiceInicio)
            throw new InvalidOperationException("UserSecretsId no encontrado.");

        return csproj[(indiceInicio + inicio.Length)..indiceFin];
    }

    private static string ExtraerPackageVersion(string csproj, string packageName)
    {
        var include = $"Include=\"{packageName}\"";
        var indiceInclude = csproj.IndexOf(include, StringComparison.Ordinal);

        if (indiceInclude < 0)
            throw new InvalidOperationException($"PackageReference no encontrado: {packageName}");

        var indiceVersion = csproj.IndexOf("Version=\"", indiceInclude, StringComparison.Ordinal);
        if (indiceVersion < 0)
            throw new InvalidOperationException($"Version no encontrada para: {packageName}");

        indiceVersion += "Version=\"".Length;
        var indiceFin = csproj.IndexOf('"', indiceVersion);
        if (indiceFin <= indiceVersion)
            throw new InvalidOperationException($"Version invalida para: {packageName}");

        return csproj[indiceVersion..indiceFin];
    }
}
