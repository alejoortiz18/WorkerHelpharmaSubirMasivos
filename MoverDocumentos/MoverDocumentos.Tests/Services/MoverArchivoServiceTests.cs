using FluentAssertions;
using MoverDocumentos.Core.Services;
using Xunit;

namespace MoverDocumentos.Tests.Services;

public class MoverArchivoServiceTests
{
    [Fact]
    public void ResolverRutaDestinoSinSobrescribir_GeneraSufijosIncrementales()
    {
        var carpeta = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(carpeta);

        try
        {
            File.WriteAllText(Path.Combine(carpeta, "Factura.pdf"), "a");
            File.WriteAllText(Path.Combine(carpeta, "Factura(1).pdf"), "b");

            var tercero = MoverArchivoService.ResolverRutaDestinoSinSobrescribir(carpeta, "Factura.pdf");

            Path.GetFileName(tercero).Should().Be("Factura(2).pdf");
        }
        finally
        {
            Directory.Delete(carpeta, recursive: true);
        }
    }
}
