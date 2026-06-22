using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;
using NSubstitute;
using Xunit;

namespace Tests.Infrastructure;

public class IronBarcodeLicenseInitializerTests
{
    private static IOptions<IronBarcodeSettings> BuildOptions(string? key) =>
        Options.Create(new IronBarcodeSettings { LicenseKey = key! });

    // ─── Validación de clave vacía / nula ──────────────────────────────────

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_LicenseKeyAusenteOVacia_LogsCritical(string? key)
    {
        // Arrange
        var logger = Substitute.For<ILogger<IronBarcodeLicenseInitializer>>();
        var options = BuildOptions(key);

        // Act
        var act = () => new IronBarcodeLicenseInitializer(options, logger);

        // Assert - no debe lanzar excepción
        act.Should().NotThrow();

        // Debe loguear Critical exactamente una vez
        logger.Received(1).Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_LicenseKeyAusenteOVacia_NoLanzaExcepcion(string? key)
    {
        // Arrange
        var logger = Substitute.For<ILogger<IronBarcodeLicenseInitializer>>();
        var options = BuildOptions(key);

        // Act & Assert
        var act = () => new IronBarcodeLicenseInitializer(options, logger);
        act.Should().NotThrow();
    }

    // ─── Validación de clave inválida (key con valor pero incorrecta) ──────

    [Fact]
    public void Constructor_LicenseKeyInvalida_LogsCritical()
    {
        // Arrange
        var logger = Substitute.For<ILogger<IronBarcodeLicenseInitializer>>();
        var options = BuildOptions("CLAVE-INVALIDA-123");

        // Act
        var act = () => new IronBarcodeLicenseInitializer(options, logger);

        // Assert - no debe lanzar excepción
        act.Should().NotThrow();

        // Debe loguear Critical (licencia inválida)
        logger.Received(1).Log(
            LogLevel.Critical,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    // ─── No lanza excepción con config nula ────────────────────────────────

    [Fact]
    public void Constructor_ConfigValueNulo_NoLanzaExcepcion()
    {
        // Arrange
        var logger = Substitute.For<ILogger<IronBarcodeLicenseInitializer>>();
        var options = Options.Create(new IronBarcodeSettings());

        // Act & Assert
        var act = () => new IronBarcodeLicenseInitializer(options, logger);
        act.Should().NotThrow();
    }
}
