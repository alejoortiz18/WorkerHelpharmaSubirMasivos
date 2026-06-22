using FluentAssertions;
using GestionArchivosEscaneados.Infrastructure.Auth;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using GestionArchivosEscaneados.Models.Dto;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace GestionArchivosEscaneados.Tests;

public class UsuarioAuthServiceTests
{
    [Fact]
    public async Task Login_AceptaEquivalenciaUppercase()
    {
        var service = CrearServicioConUsuarios("alejandro.ortiz");

        var resultado = await service.ValidarLoginAsync("ALEJANDRO.ORTIZ");

        resultado.Estado.Should().Be(ValidacionLoginEstado.Exito);
        resultado.UsuarioNormalizado.Should().Be("alejandro.ortiz");
    }

    [Fact]
    public async Task Login_AceptaCorreoComoWorker1()
    {
        var service = CrearServicioConUsuarios("alejandro.ortiz");

        var resultado = await service.ValidarLoginAsync("alejandro.ortiz@zentria.com.co");

        resultado.Estado.Should().Be(ValidacionLoginEstado.Exito);
        resultado.UsuarioNormalizado.Should().Be("alejandro.ortiz");
    }

    [Fact]
    public async Task Login_AceptaDominioBarraUsuario()
    {
        var service = CrearServicioConUsuarios("alejandro.ortiz");

        var resultado = await service.ValidarLoginAsync(@"ZENTRIA\alejandro.ortiz");

        resultado.Estado.Should().Be(ValidacionLoginEstado.Exito);
        resultado.UsuarioNormalizado.Should().Be("alejandro.ortiz");
    }

    [Fact]
    public async Task Login_UsuarioInexistente_RetornaNoRegistrado()
    {
        var service = CrearServicioConUsuarios("alejandro.ortiz");

        var resultado = await service.ValidarLoginAsync("otro.usuario");

        resultado.Estado.Should().Be(ValidacionLoginEstado.UsuarioNoRegistrado);
    }

    private static UsuarioAuthService CrearServicioConUsuarios(params string[] usuarios)
    {
        var trazabilidad = Substitute.For<ITrazabilidadConsultaSqlService>();
        trazabilidad.UsuarioExisteAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var usuario = callInfo.Arg<string>().Trim();
                return usuarios.Contains(usuario, StringComparer.OrdinalIgnoreCase);
            });

        return new UsuarioAuthService(
            trazabilidad,
            NullLogger<UsuarioAuthService>.Instance);
    }
}
