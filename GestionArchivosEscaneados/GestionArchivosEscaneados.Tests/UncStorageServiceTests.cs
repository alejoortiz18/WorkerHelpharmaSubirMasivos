using FluentAssertions;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GestionArchivosEscaneados.Tests;

public class UncStorageServiceTests
{
    [Fact]
    public void ListarNoProcesados_ReflejaIntentoPrevio()
    {
        var (service, root) = CrearServicio();
        try
        {
            var usuario = "alejandro";
            var fecha = "2026-06-16";
            var rutaPdf = CrearPdfNoProcesado(root, usuario, fecha, "archivo1.pdf");

            service.MarcarIntentoPrevio(usuario, fecha, Path.GetFileName(rutaPdf));

            var archivos = service.ListarNoProcesados(usuario, fecha);

            archivos.Should().ContainSingle();
            archivos[0].NombreArchivo.Should().Be("archivo1.pdf");
            archivos[0].TieneIntentoPrevio.Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void EliminarPdfNoProcesado_LimpiaMarcadorDeIntentoPrevio()
    {
        var (service, root) = CrearServicio();
        try
        {
            var usuario = "alejandro";
            var fecha = "2026-06-16";
            var rutaPdf = CrearPdfNoProcesado(root, usuario, fecha, "archivo2.pdf");
            var rutaMarcador = rutaPdf + ".attempt";
            File.WriteAllText(rutaMarcador, "ok");

            var eliminado = service.EliminarPdfNoProcesado(usuario, fecha, "archivo2.pdf");

            eliminado.Should().BeTrue();
            File.Exists(rutaPdf).Should().BeFalse();
            File.Exists(rutaMarcador).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static (UncStorageService Service, string Root) CrearServicio()
    {
        var root = Path.Combine(Path.GetTempPath(), "gae-unc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var rutas = new RutasSettings
        {
            RaizUnc = root
        };

        var config = new MapIntegracionConfigProvider(rutas);
        var unc = new UncConexionService(config, NullLogger<UncConexionService>.Instance);

        return (new UncStorageService(config, unc), root);
    }

    private static string CrearPdfNoProcesado(string root, string usuario, string fecha, string nombreArchivo)
    {
        var carpeta = Path.Combine(root, usuario, fecha, "noprocesados");
        Directory.CreateDirectory(carpeta);
        var rutaPdf = Path.Combine(carpeta, nombreArchivo);
        File.WriteAllText(rutaPdf, "%PDF-1.4");
        return rutaPdf;
    }
}
