using FluentAssertions;
using MoverDocumentos.Core.Services;
using Xunit;

namespace MoverDocumentos.Tests.Services;

public class EmailNotificationServiceTests
{
    [Fact]
    public void ConstruirCuerpoEsperado_UsaMismoFormatoQueFalloOpenAi()
    {
        var usuario = "alejandro.ortiz";
        var fecha = new DateOnly(2026, 6, 4);
        var ruta = @"\\192.168.0.69\ArchivosScaneados\alejandro.ortiz\2026-06-04\procesar";
        var error = "No se puede acceder a la ruta UNC";

        var cuerpo = EmailNotificationService.ConstruirCuerpoEsperado(
            usuario,
            fecha,
            3,
            ruta,
            error);

        cuerpo.Should().Contain("El usuario alejandro.ortiz ha escaneado 3 archivos del día 2026-06-04");
        cuerpo.Should().Contain("al subirlos a la NAS (procesar) se presentó el siguiente error:");
        cuerpo.Should().Contain($"Error:{Environment.NewLine}{error}");
        cuerpo.Should().Contain($"Ruta:{Environment.NewLine}{ruta}");
    }
}
