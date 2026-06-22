using FluentAssertions;
using Infrastructure;
using Xunit;

namespace Tests.Infrastructure;

public class FileManagerInfraestructureTests
{
    private const string Prefijo = "CRC_900277244_";

    [Theory]
    [InlineData("documento.pdf", "CRC_900277244_documento.pdf")]
    [InlineData("CRC_900277244_documento.pdf", "CRC_900277244_documento.pdf")]
    [InlineData("crc_900277244_documento.pdf", "CRC_900277244_documento.pdf")]
    [InlineData(
        "CRC_900277244_CRC_900277244_CRC_900277244_26052026_0048.pdf",
        "CRC_900277244_26052026_0048.pdf")]
    public void NormalizarNombreConPrefijo_DejaUnSoloPrefijo(string nombre, string esperado)
    {
        var resultado = FileManagerInfraestructure.NormalizarNombreConPrefijo(nombre, Prefijo);

        resultado.Should().Be(esperado);
    }
}
