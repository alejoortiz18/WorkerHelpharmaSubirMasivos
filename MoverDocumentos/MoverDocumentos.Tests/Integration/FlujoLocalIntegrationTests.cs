using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MoverDocumentos.Core.Configuration;
using MoverDocumentos.Core.Services;
using Xunit;

namespace MoverDocumentos.Tests.Integration;

public class FlujoLocalIntegrationTests : IDisposable
{
    private readonly string _raiz;
    private readonly string _scaneo;
    private readonly RutasSettings _rutas;

    public FlujoLocalIntegrationTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "MoverDocumentosTest_" + Guid.NewGuid().ToString("N"));
        _scaneo = Path.Combine(_raiz, "scaneo");
        Directory.CreateDirectory(_scaneo);

        _rutas = new RutasSettings
        {
            CarpetaLocal = _scaneo,
            RaizUnc = _raiz,
            CarpetaArchivosNuevos = "ArchivosNuevos",
            CarpetaUsuarios = "Usuarios",
            ArchivoUsuarios = "usuarios.txt"
        };
    }

    [Fact]
    public async Task FlujoCompleto_MuevePdf_RegistraUsuario_YGeneraTxt()
    {
        var usuario = "usuario.prueba";
        var fecha = DateOnly.FromDateTime(new DateTime(2026, 6, 2));

        var estructura = CrearEstructura();
        var mover = new MoverArchivoService(NullLogger<MoverArchivoService>.Instance);
        var registro = CrearRegistro();
        var lote = CrearLote(segundosInactividad: 1);

        var origen = Path.Combine(_scaneo, "Factura.pdf");
        await File.WriteAllBytesAsync(origen, "%PDF-1.4 prueba"u8.ToArray());
        await Task.Delay(200);

        var carpetaProcesar = estructura.CrearEstructuraDia(usuario, fecha);
        registro.RegistrarSiNoExiste(usuario);
        mover.Mover(origen, carpetaProcesar);
        lote.RegistrarMovimiento(usuario, fecha, carpetaProcesar);

        await Task.Delay(2500);

        File.Exists(origen).Should().BeFalse();
        Directory.GetFiles(carpetaProcesar, "*.pdf").Should().HaveCount(1);

        var usuariosPath = _rutas.RutaArchivoUsuarios;
        File.Exists(usuariosPath).Should().BeTrue();
        File.ReadAllText(usuariosPath).Should().Contain(usuario);

        var txts = Directory.GetFiles(_rutas.RutaArchivosNuevos, "*.txt");
        txts.Should().NotBeEmpty();
        File.ReadAllText(txts[0]).Trim().Should().Be(carpetaProcesar);
    }

    private EstructuraCarpetasService CrearEstructura() =>
        new(Options.Create(_rutas), NullLogger<EstructuraCarpetasService>.Instance);

    private RegistroUsuarioService CrearRegistro() =>
        new(Options.Create(_rutas), NullLogger<RegistroUsuarioService>.Instance);

    private LoteService CrearLote(int segundosInactividad) =>
        new(
            Options.Create(_rutas),
            Options.Create(new LoteSettings { SegundosInactividadParaCerrarLote = segundosInactividad }),
            NullLogger<LoteService>.Instance);

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, recursive: true);
    }
}
