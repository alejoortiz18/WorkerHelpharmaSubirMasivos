using FluentAssertions;
using GestionArchivosEscaneados.Infrastructure.Auth;

namespace GestionArchivosEscaneados.Tests;

public class UsuarioNormalizadorTests
{
    [Theory]
    [InlineData("alejandro.ortiz", "alejandro.ortiz")]
    [InlineData("ALEJANDRO.ORTIZ", "alejandro.ortiz")]
    [InlineData("alejandro.ortiz@zentria.com.co", "alejandro.ortiz")]
    [InlineData(@"ZENTRIA\alejandro.ortiz", "alejandro.ortiz")]
    public void NormalizarIngreso_ExtraeUsuarioLocal(string entrada, string esperado)
    {
        UsuarioNormalizador.NormalizarIngreso(entrada).Should().Be(esperado);
    }
}
