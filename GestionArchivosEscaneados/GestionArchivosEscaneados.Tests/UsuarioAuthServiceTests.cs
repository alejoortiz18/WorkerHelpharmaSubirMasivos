using FluentAssertions;
using GestionArchivosEscaneados.Infrastructure.Auth;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GestionArchivosEscaneados.Tests;

public class UsuarioAuthServiceTests
{
    [Fact]
    public async Task Login_AceptaEquivalenciaUppercase()
    {
        var dir = Path.Combine(Path.GetTempPath(), "gae-auth-" + Guid.NewGuid().ToString("N"));
        var usuariosDir = Path.Combine(dir, "Usuarios");
        Directory.CreateDirectory(usuariosDir);
        await File.WriteAllTextAsync(Path.Combine(usuariosDir, "usuarios.txt"), "alejandro.ortiz\n");

        var rutas = new RutasSettings
        {
            RaizUnc = dir,
            CarpetaUsuarios = "Usuarios",
            ArchivoUsuarios = "usuarios.txt"
        };

        var service = new UsuarioAuthService(Options.Create(rutas), NullLogger<UsuarioAuthService>.Instance);

        var resultado = await service.ValidarYObtenerUsuarioNormalizadoAsync("ALEJANDRO.ORTIZ");
        resultado.Should().Be("alejandro.ortiz");

        Directory.Delete(dir, true);
    }
}
