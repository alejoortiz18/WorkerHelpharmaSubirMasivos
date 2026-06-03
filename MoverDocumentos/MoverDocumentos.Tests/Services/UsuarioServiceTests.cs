using FluentAssertions;
using MoverDocumentos.Core.Services;
using Xunit;

namespace MoverDocumentos.Tests.Services;

public class UsuarioServiceTests
{
    [Theory]
    [InlineData("Alejandro.Ortiz@zentria.com.co", "alejandro.ortiz")]
    [InlineData("alejandro.ortiz.gaviria@zentria.com.co", "alejandro.ortiz.gaviria")]
    [InlineData("ALEJANDRO.ORTIZ@ZENTRIA.COM.CO", "alejandro.ortiz")]
    [InlineData("DOMINIO\\Alejandro.Ortiz", "alejandro.ortiz")]
    public void NormalizarDesdeCorreo_ExtraeParteLocalEnMinusculas(string entrada, string esperado)
    {
        var resultado = UsuarioService.NormalizarDesdeCorreo(entrada);

        resultado.Should().Be(esperado);
    }
}
