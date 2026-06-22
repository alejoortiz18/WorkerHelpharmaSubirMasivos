using FluentAssertions;
using Models.Dto;
using Services;
using Xunit;

namespace Tests.Infrastructure;

public class OpenAiBarcodeServiceTests
{
    [Theory]
    [InlineData("KI-470549", "KI", "470549", "KI470549")]
    [InlineData("KE-470554", "KE", "470554", "KE470554")]
    [InlineData("FBO79606", "FBO", "79606", "FBO79606")]
    [InlineData("*KI-434411*", "KI", "434411", "KI434411")]
    [InlineData("NO_BARCODE", null, null, null)]
    [InlineData("texto invalido", null, null, null)]
    public void InterpretarRespuesta_NormalizaCodigoBajoBarcode(
        string entrada,
        string? prefijo,
        string? numero,
        string? codigo)
    {
        var resultado = OpenAiBarcodeService.InterpretarRespuesta(entrada);

        if (codigo == null)
        {
            resultado.Tipo.Should().Be(OpenAiBarcodeResultKind.NoBarcode);
            return;
        }

        resultado.Tipo.Should().Be(OpenAiBarcodeResultKind.CodigoEncontrado);
        resultado.Codigo.Should().Be(codigo);
        resultado.Documento!.Prefijo.Should().Be(prefijo);
        resultado.Documento.Numero.Should().Be(numero);
        resultado.Documento.NombreArchivo.Should().Be($"{codigo}.pdf");
    }
}
