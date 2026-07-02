using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GestionArchivosEscaneados.Application;
using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Infrastructure.Api;
using GestionArchivosEscaneados.Infrastructure.Barcode;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Infrastructure.Trazabilidad;
using GestionArchivosEscaneados.Infrastructure.Unc;
using GestionArchivosEscaneados.Models.Entities;
using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Models.Enums;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace GestionArchivosEscaneados.Tests.Application;

public class ReprocesoAppServiceTests
{
    [Fact]
    public async Task ReprocesarAsync_MarcaIntentoPrevioAntesDeLeerBarcode()
    {
        var (service, root, _, _) = CrearServicio();
        try
        {
            var usuario = "alejandro.ortiz";
            var fecha = "2026-06-04";
            var nombreArchivo = "marcador.pdf";
            CrearPdfNoProcesado(root, usuario, fecha, nombreArchivo);

            await service.ReprocesarAsync(usuario, fecha, nombreArchivo, string.Empty);

            var marcador = Path.Combine(root, usuario, fecha, "noprocesados", nombreArchivo + ".attempt");
            File.Exists(marcador).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ReprocesarAsync_CuandoBarcodeFalla_UsaOpenAiYFlujoMasivosWorker()
    {
        var (service, root, handler, _) = CrearServicio();
        try
        {
            var usuario = "alejandro.ortiz";
            var fecha = "2026-06-04";
            var nombreArchivo = "archivo-prueba.pdf";
            CrearPdfNoProcesado(root, usuario, fecha, nombreArchivo);

            var resultado = await service.ReprocesarAsync(usuario, fecha, nombreArchivo, string.Empty);

            resultado.Should().Be(SoporteProcesamientoEstado.Exito);

            handler.UltimoJsonDatos.Should().Contain("\"soporte\":\"KE-470549\"");
            handler.UltimoMultipartFisico.Should().Contain("name=soporte");
            handler.UltimoMultipartFisico.Should().Contain("KE470549");
            handler.UltimoMultipartFisico.Should().NotContain("KE-470549");
            handler.UltimoMultipartFisico.Should().Contain("name=idConvenio");
            handler.UltimoMultipartFisico.Should().Contain("\r\n\r\n1\r\n");
            handler.UltimoMultipartFisico.Should().NotContain("\r\n\r\n01\r\n");
            handler.UltimoMultipartFisico.Should().Contain("name=idUsuario");
            handler.UltimoMultipartFisico.Should().Contain("system");

            File.Exists(Path.Combine(root, usuario, fecha, "noprocesados", nombreArchivo)).Should().BeFalse();
            File.Exists(Path.Combine(root, usuario, fecha, "procesados", nombreArchivo)).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ProcesarConCodigoConocidoAsync_ActualizaDocumentoUsandoUsuarioFechaYArchivo()
    {
        var (service, root, _, trazabilidad) = CrearServicio();
        try
        {
            var usuario = "alejandro.ortiz";
            var fecha = "2026-06-04";
            var nombreArchivo = "manual.pdf";
            CrearPdfNoProcesado(root, usuario, fecha, nombreArchivo);

            var resultado = await service.ProcesarConCodigoConocidoAsync(
                usuario,
                fecha,
                nombreArchivo,
                "KE-470549");

            resultado.Should().Be(SoporteProcesamientoEstado.Exito);
            await trazabilidad.Received(1).MarcarDocumentoProcesadoAsync(
                usuario,
                fecha,
                nombreArchivo,
                "KE-470549",
                1,
                "B1",
                "C1",
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ListarNoProcesadosAsync_OcultaPendientesSinPdfFisico()
    {
        var (service, root, _, trazabilidad) = CrearServicio();
        try
        {
            var usuario = "alejandro.ortiz";
            var fecha = "2026-06-04";
            CrearPdfNoProcesado(root, usuario, fecha, "visible.pdf");

            trazabilidad.ListarDocumentosPendientesAsync(
                    usuario,
                    fecha,
                    Arg.Any<CancellationToken>())
                .Returns(new[]
                {
                    new DocumentoPendiente
                    {
                        NombreArchivo = "visible.pdf"
                    },
                    new DocumentoPendiente
                    {
                        NombreArchivo = "fantasma.pdf"
                    }
                });

            var archivos = await service.ListarNoProcesadosAsync(usuario, fecha);

            archivos.Should().ContainSingle();
            archivos[0].NombreArchivo.Should().Be("visible.pdf");
            archivos[0].TieneIntentoPrevio.Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static (ReprocesoAppService Service, string Root, CapturingHandler Handler, ITrazabilidadConsultaSqlService Trazabilidad) CrearServicio()
    {
        var root = Path.Combine(Path.GetTempPath(), "gae-reproceso-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        var rutas = new RutasSettings { RaizUnc = root };
        var uncConfig = new MapIntegracionConfigProvider(rutas);
        var uncConexion = new UncConexionService(uncConfig, NullLogger<UncConexionService>.Instance);
        var unc = new UncStorageService(uncConfig, uncConexion);
        var trazabilidad = Substitute.For<ITrazabilidadConsultaSqlService>();
        trazabilidad.MarcarDocumentoProcesadoAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<int?>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<DateTime?>(),
                Arg.Any<CancellationToken>())
            .Returns(true);
        trazabilidad.ListarDocumentosPendientesAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DocumentoPendiente>());
        trazabilidad.DocumentoPendienteExisteAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(true);

        var barcode = Substitute.For<IBarcodeRegionService>();
        barcode.LeerCodigoDesdePdf(Arg.Any<string>()).Returns((string?)null);

        var openAi = Substitute.For<IOpenAiBarcodeService>();
        openAi.LeerCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.CodigoEncontrado,
                Codigo = "KE-470549"
            });

        var handler = new CapturingHandler();
        var config = Substitute.For<IIntegracionConfigProvider>();
        config.ObtenerFallback(Arg.Any<string>()).Returns(string.Empty);
        config.ObtenerProductoAsync(ProductoIntegracion.SoporteApi, Arg.Any<CancellationToken>())
            .Returns(new ConfiguracionProducto
            {
                Producto = ProductoIntegracion.SoporteApi,
                Endpoint = IntegracionDefaults.SoporteApiUrl,
                ClaveCredencial = "test-api-key"
            });
        config.ObtenerProductoAsync(ProductoIntegracion.SoporteFisico, Arg.Any<CancellationToken>())
            .Returns(new ConfiguracionProducto
            {
                Producto = ProductoIntegracion.SoporteFisico,
                Endpoint = IntegracionDefaults.SoporteFisicoApiUrl,
                ClaveCredencial = "test-token",
                ValorAdicional = "system"
            });

        var soporteApi = new SoporteApiService(
            new HttpClient(handler),
            config,
            NullLogger<SoporteApiService>.Instance);

        var soporteFisicoApi = new SoporteFisicoApiService(
            new HttpClient(handler),
            config,
            NullLogger<SoporteFisicoApiService>.Instance);

        var soporte = new SoporteProcesamientoService(
            soporteApi,
            soporteFisicoApi,
            NullLogger<SoporteProcesamientoService>.Instance);

        var service = new ReprocesoAppService(
            unc,
            trazabilidad,
            barcode,
            openAi,
            soporte,
            Options.Create(new FileSettings
            {
                BarcodeMaxReintentos = 1,
                BarcodeEsperaMs = 1
            }),
            NullLogger<ReprocesoAppService>.Instance);

        return (service, root, handler, trazabilidad);
    }

    private static void CrearPdfNoProcesado(string root, string usuario, string fecha, string nombreArchivo)
    {
        var carpeta = Path.Combine(root, usuario, fecha, "noprocesados");
        Directory.CreateDirectory(carpeta);
        File.WriteAllBytes(Path.Combine(carpeta, nombreArchivo), Encoding.UTF8.GetBytes("%PDF-1.4\n% prueba"));
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string UltimoJsonDatos { get; private set; } = string.Empty;

        public string UltimoMultipartFisico { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var content = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);

            if (request.RequestUri?.AbsoluteUri.Contains("DatosSoportes", StringComparison.OrdinalIgnoreCase) == true)
            {
                UltimoJsonDatos = content;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""
                    {
                      "IdConvenio": "1",
                      "NombreConvenio": "Convenio prueba",
                      "Fecha": "2026-06-04T00:00:00",
                      "IdBodega": "B1",
                      "NombreSede": "Sede prueba",
                      "NombreActividad": "Actividad",
                      "TipoEntrega": "Entrega",
                      "TipoPlan": "Plan",
                      "IdCartera": "C1",
                      "NombrePaciente": "Paciente prueba",
                      "IdTipoId": "CC",
                      "IdPaciente": "1",
                      "Celular": "3000000000",
                      "Telefono": "3000000001",
                      "Direccion": "Direccion",
                      "Complemento": "",
                      "Observacion": "",
                      "ValorCM": "0",
                      "medicamentos": []
                    }
                    """, Encoding.UTF8, "application/json")
                };
            }

            if (request.RequestUri?.AbsoluteUri.Contains("/soporte/fisico", StringComparison.OrdinalIgnoreCase) == true)
            {
                UltimoMultipartFisico = content;
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("true", Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }
    }
}
