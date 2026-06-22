using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Models;
using Xunit;

namespace Tests.Infrastructure;

public class UsuarioArchivoResolverTests
{
    [Theory]
    [InlineData("usuariopik-2026-06-09 09-27-15AM.txt", "usuariopik")]
    [InlineData("dgutierrez-2026-06-09 07-29-15AM.txt", "dgutierrez")]
    [InlineData("alejandro.ortiz-2026-06-09 09-29-15AM.txt", "alejandro.ortiz")]
    public void Resolver_TomaTextoAntesDelPrimerGuion(string nombre, string esperado)
    {
        UsuarioArchivoResolver.Resolver(nombre).Should().Be(esperado);
    }

    [Fact]
    public void Resolver_AceptaRutaCompleta()
    {
        var ruta = Path.Combine("C:", "ArchivosNuevos", "USUARIOPIK-2026-06-09 09-27-15AM.txt");
        UsuarioArchivoResolver.Resolver(ruta).Should().Be("usuariopik");
    }

    [Fact]
    public void Resolver_SinGuion_UsaNombreSinExtension()
    {
        UsuarioArchivoResolver.Resolver("usuariosolo.txt").Should().Be("usuariosolo");
    }
}

public class RegistroUsuariosEnProcesoServiceTests
{
    private static RegistroUsuariosEnProcesoService Crear() =>
        new(NullLogger<RegistroUsuariosEnProcesoService>.Instance);

    [Fact]
    public void IntentarRegistrar_PrimeraVez_RegistraYMarcaActivo()
    {
        var registro = Crear();

        registro.IntentarRegistrar("usuariopik").Should().BeTrue();
        registro.EstaActivo("usuariopik").Should().BeTrue();
        registro.Activos.Should().Be(1);
    }

    [Fact]
    public void IntentarRegistrar_UsuarioYaActivo_DevuelveFalse()
    {
        var registro = Crear();
        registro.IntentarRegistrar("usuariopik");

        registro.IntentarRegistrar("usuariopik").Should().BeFalse();
        registro.Activos.Should().Be(1);
    }

    [Fact]
    public void Liberar_PermiteVolverARegistrar()
    {
        var registro = Crear();
        registro.IntentarRegistrar("usuariopik");

        registro.Liberar("usuariopik");

        registro.EstaActivo("usuariopik").Should().BeFalse();
        registro.IntentarRegistrar("usuariopik").Should().BeTrue();
    }

    [Fact]
    public void IntentarRegistrar_EsAtomicoBajoConcurrencia()
    {
        var registro = Crear();
        var exitos = 0;

        Parallel.For(0, 100, _ =>
        {
            if (registro.IntentarRegistrar("usuariopik"))
                Interlocked.Increment(ref exitos);
        });

        exitos.Should().Be(1, "solo un hilo puede tomar al usuario a la vez");
        registro.Activos.Should().Be(1);
    }
}
