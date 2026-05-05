using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Services;
using Xunit;

namespace Tests.Services;

/// <summary>
/// Pruebas unitarias para la lógica de parsing de códigos en BarcodeRegionService.
/// Los métodos que dependen de IronPdf/IronBarCode se prueban con archivos reales en TestData/.
/// </summary>
public class BarcodeRegionServiceTests
{
    private readonly BarcodeRegionService _sut;

    public BarcodeRegionServiceTests()
    {
        _sut = new BarcodeRegionService(new NullLogger<BarcodeRegionService>());
    }

    // ─── ProcesarPdf – validaciones de entrada ─────────────────────────────

    [Fact]
    public void ProcesarPdf_ArchivoNoExiste_RetornaNull()
    {
        // Arrange
        const string rutaInexistente = "C:/ruta/que/no/existe.pdf";

        // Act
        var resultado = _sut.ProcesarPdf(rutaInexistente);

        // Assert
        resultado.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ProcesarPdf_RutaNulaOVacia_RetornaNull(string? ruta)
    {
        // Act
        var resultado = _sut.ProcesarPdf(ruta!);

        // Assert
        resultado.Should().BeNull();
    }
}
