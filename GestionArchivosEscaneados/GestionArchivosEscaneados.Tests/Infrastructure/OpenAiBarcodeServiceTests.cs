using GestionArchivosEscaneados.Infrastructure.Barcode;
using Xunit;

namespace GestionArchivosEscaneados.Tests.Infrastructure;

public class OpenAiBarcodeServiceTests
{
    [Theory]
    [InlineData("ME-123", "ME123")]
    [InlineData("me 123", "ME123")]
    [InlineData("ME123", "ME123")]
    public void InterpretarRespuesta_NormalizaCodigoValido(string respuesta, string esperado)
    {
        var resultado = OpenAiBarcodeService.InterpretarRespuesta(respuesta);

        Assert.Equal(OpenAiBarcodeResultKind.CodigoEncontrado, resultado.Tipo);
        Assert.Equal(esperado, resultado.Codigo);
    }

    [Theory]
    [InlineData("NO_BARCODE")]
    [InlineData("sin codigo")]
    [InlineData("")]
    public void InterpretarRespuesta_DetectaAusenciaDeCodigo(string respuesta)
    {
        var resultado = OpenAiBarcodeService.InterpretarRespuesta(respuesta);

        Assert.Equal(OpenAiBarcodeResultKind.NoBarcode, resultado.Tipo);
        Assert.Null(resultado.Codigo);
    }
}
