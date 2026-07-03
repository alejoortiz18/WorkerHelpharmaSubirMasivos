using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;
using NSubstitute;
using Services;
using Xunit;

namespace Tests.Infrastructure;

/// <summary>
/// Escenario de prueba Worker 2: asume carpetas, PDF y TXT ya existentes (sin Worker 1 ni MVC).
/// </summary>
public sealed class Worker2Escenario : IDisposable
{
    public string Raiz { get; }

    public string ArchivosNuevos { get; }

    public RutasLoteContext Contexto { get; }

    public Worker2Escenario(string usuario = "test.user", string fecha = "2026-06-03")
    {
        Raiz = Path.Combine(Path.GetTempPath(), "worker2-" + Guid.NewGuid().ToString("N"));
        ArchivosNuevos = Path.Combine(Raiz, "ArchivosNuevos");

        var carpetaDia = Path.Combine(Raiz, usuario, fecha);
        Contexto = new RutasLoteContext
        {
            Usuario = usuario,
            Fecha = fecha,
            Procesar = Path.Combine(carpetaDia, "procesar"),
            Procesando = Path.Combine(carpetaDia, "procesando"),
            Error = Path.Combine(carpetaDia, "error"),
            Procesaria = Path.Combine(carpetaDia, "procesaria"),
            Noprocesados = Path.Combine(carpetaDia, "noprocesados"),
            Procesados = Path.Combine(carpetaDia, "procesados")
        };

        Directory.CreateDirectory(ArchivosNuevos);
        foreach (var carpeta in Contexto.CarpetasOperativas)
            Directory.CreateDirectory(carpeta);
    }

    public string CrearPdfEnProcesar(string nombre)
    {
        var ruta = Path.Combine(Contexto.Procesar, nombre);
        File.WriteAllBytes(ruta, [0x25, 0x50, 0x44, 0x46]); // bytes mínimos .pdf
        return ruta;
    }

    public string CrearTxtLote(string? nombreTxt = null)
    {
        nombreTxt ??= $"{Contexto.Usuario}-{Contexto.Fecha} 08-42-51AM.txt";
        var rutaTxt = Path.Combine(ArchivosNuevos, nombreTxt);
        File.WriteAllText(rutaTxt, Contexto.Procesar + Environment.NewLine);
        return rutaTxt;
    }

    public LoteProcesamientoService CrearServicio(
        IDocumentoProcesamientoService documento,
        IOpenAiBarcodeService? openAi = null,
        IEmailNotificationService? email = null,
        IRadicaWebIntegracionService? radicaWeb = null,
        int tamanoLote = 3)
    {
        if (openAi is null)
        {
            openAi = Substitute.For<IOpenAiBarcodeService>();
            openAi.LeerCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(new OpenAiBarcodeResult { Tipo = OpenAiBarcodeResultKind.NoBarcode });
        }
        email ??= Substitute.For<IEmailNotificationService>();
        radicaWeb ??= new NoopRadicaWebIntegracionService();

        var fileSettings = Options.Create(new FileSettings
        {
            TamanoLote = tamanoLote,
            MaxArchivosConcurrentes = 4,
            KeyName = "",
            AplicarPrefijoKeyName = false
        });

        var fileManager = new FileManagerInfraestructure(
            fileSettings,
            NullLogger<FileManagerInfraestructure>.Instance);

        var redDisponible = new RedDisponibleService(
            Options.Create(new RutasSettings()),
            Options.Create(new RedSettings { UsarCredencialesConfiguradas = false }),
            NullLogger<RedDisponibleService>.Instance);

        return new LoteProcesamientoService(
            fileManager,
            documento,
            openAi,
            email,
            new NoopTrazabilidadSqlService(),
            radicaWeb,
            redDisponible,
            fileSettings,
            NullLogger<LoteProcesamientoService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(Raiz))
            Directory.Delete(Raiz, recursive: true);
    }
}

public class Worker2ProcesamientoTests
{
    private static DocumentoProcesadoDto Documento(string codigo) =>
        new()
        {
            Prefijo = codigo[..2],
            Numero = codigo[2..],
            NombreArchivo = $"{codigo}.pdf"
        };

    private static DocumentoProcesamientoResult Exito(string codigo = "KV351697") =>
        new() { Estado = DocumentoProcesamientoEstado.Exito, Documento = Documento(codigo) };

    private static DocumentoProcesamientoResult ExitoConRadicaWeb(
        string bodega = "FARMACIAMEDELLIN",
        string fecha = "2026-07-02",
        string codigo = "KV351697") =>
        new()
        {
            Estado = DocumentoProcesamientoEstado.Exito,
            Documento = Documento(codigo),
            IdBodega = bodega,
            FechaFactura = DateTime.ParseExact(fecha, "yyyy-MM-dd", null)
        };

    private static DocumentoProcesamientoResult FalloBarcode() =>
        new() { Estado = DocumentoProcesamientoEstado.FalloBarcode };

    private static DocumentoProcesamientoResult FalloApi() =>
        new() { Estado = DocumentoProcesamientoEstado.FalloApiDatos, Documento = Documento("KV999") };

    private static DocumentoProcesamientoResult PdfCorrupto() =>
        new() { Estado = DocumentoProcesamientoEstado.PdfCorrupto };

    [Fact]
    public async Task LoteFeliz_TresPdfProcesados_ActualizaLogYLimpiaTemporales()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("doc1.pdf");
        escenario.CrearPdfEnProcesar("doc2.pdf");
        escenario.CrearPdfEnProcesar("doc3.pdf");
        var txt = escenario.CrearTxtLote();

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Exito());

        var servicio = escenario.CrearServicio(documento);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        File.Exists(txt).Should().BeFalse("el TXT del lote debe eliminarse al cerrar");
        Directory.GetFiles(escenario.Contexto.Procesar).Should().BeEmpty();
        Directory.GetFiles(escenario.Contexto.Procesando).Should().BeEmpty();
        Directory.GetFiles(escenario.Contexto.Procesados).Should().BeEmpty("limpieza post-lote");
        Directory.GetFiles(escenario.Contexto.Error).Should().BeEmpty();

        await documento.Received(3).ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoteCompleto_InvocaRadicaWebTrasOpenAiConCombinacionesUnicas()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("doc1.pdf");
        escenario.CrearPdfEnProcesar("doc2.pdf");
        var txt = escenario.CrearTxtLote();

        var radicaWeb = new NoopRadicaWebIntegracionService();
        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ExitoConRadicaWeb());

        var servicio = escenario.CrearServicio(documento, radicaWeb: radicaWeb);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        radicaWeb.Llamadas.Should().HaveCount(1);
        radicaWeb.Llamadas[0].Combinaciones.Should().HaveCount(1);
        radicaWeb.Llamadas[0].Combinaciones[0].Should().Be((new DateOnly(2026, 7, 2), "FARMACIAMEDELLIN"));
    }

    [Fact]
    public async Task LoteConIncidenciaInfraestructura_InvocaRadicaWebAntesDePendienteReintento()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("doc1.pdf");
        escenario.CrearPdfEnProcesar("doc2.pdf");
        var txt = escenario.CrearTxtLote();

        var radicaWeb = new NoopRadicaWebIntegracionService();
        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => ExitoConRadicaWeb(),
                _ => throw new IOException("fallo de red simulado"));

        var servicio = escenario.CrearServicio(documento, radicaWeb: radicaWeb, tamanoLote: 1);

        var outcome = await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        outcome.Estado.Should().Be(LoteProcesamientoEstado.PendienteReintento);
        radicaWeb.Llamadas.Should().HaveCount(1, "RadicaWeb debe ejecutarse tras los 3 intentos aunque haya incidencia de infraestructura");
        radicaWeb.Llamadas[0].Combinaciones.Should().NotBeEmpty();
        File.Exists(txt).Should().BeTrue("el TXT no debe eliminarse en PendienteReintento");
    }

    [Fact]
    public async Task LoteSinExitosos_NoInvocaRadicaWeb()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("doc1.pdf");
        var txt = escenario.CrearTxtLote();

        var radicaWeb = new NoopRadicaWebIntegracionService();
        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FalloApi());

        var openAi = Substitute.For<IOpenAiBarcodeService>();
        openAi.LeerCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new OpenAiBarcodeResult { Tipo = OpenAiBarcodeResultKind.NoBarcode });

        var servicio = escenario.CrearServicio(documento, openAi, radicaWeb: radicaWeb);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        radicaWeb.Llamadas.Should().BeEmpty();
    }

    [Fact]
    public async Task Tandas_SietePdf_ConTamanoLote3_ProcesaTodos()
    {
        using var escenario = new Worker2Escenario();
        for (int i = 1; i <= 7; i++)
            escenario.CrearPdfEnProcesar($"doc{i}.pdf");

        var txt = escenario.CrearTxtLote();

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Exito());

        var servicio = escenario.CrearServicio(documento, tamanoLote: 3);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        Directory.GetFiles(escenario.Contexto.Procesar).Should().BeEmpty();
        await documento.Received(7).ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());

    }

    [Fact]
    public async Task Intento1_FalloBarcode_Intento2_Exito_Procesados()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("ilegible.pdf");
        var txt = escenario.CrearTxtLote();

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FalloBarcode());
        documento.ProcesarConCodigoConocidoAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Exito("KV111111"));

        var openAi = Substitute.For<IOpenAiBarcodeService>();
        openAi.LeerCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.CodigoEncontrado,
                Codigo = "KV111111",
                Documento = Documento("KV111111")
            });

        var servicio = escenario.CrearServicio(documento, openAi);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        await documento.Received(1).ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await openAi.Received(1).LeerCodigoAsync(
            Arg.Is<string>(r => r.Contains("procesaria")),
            Arg.Any<CancellationToken>());

        Directory.GetFiles(escenario.Contexto.Noprocesados).Should().BeEmpty();
        Directory.GetFiles(escenario.Contexto.Procesaria).Should().BeEmpty();
        Directory.GetFiles(escenario.Contexto.Procesados).Should().BeEmpty("la limpieza post-lote borra procesados");
    }

    [Fact]
    public async Task FalloApi_VaDirectoANoprocesados_SinPasarPorError()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("soporte-invalido.pdf");
        var txt = escenario.CrearTxtLote();

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FalloApi());

        var servicio = escenario.CrearServicio(documento);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        Directory.GetFiles(escenario.Contexto.Error).Should().BeEmpty();
        Directory.GetFiles(escenario.Contexto.Noprocesados).Should().HaveCount(1);

    }

    [Fact]
    public async Task PdfCorrupto_VaDirectoANoprocesados()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("corrupto.pdf");
        var txt = escenario.CrearTxtLote();

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(PdfCorrupto());

        var servicio = escenario.CrearServicio(documento);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        Directory.GetFiles(escenario.Contexto.Noprocesados).Should().ContainSingle()
            .Which.Should().EndWith("corrupto.pdf");
        Directory.GetFiles(escenario.Contexto.Error).Should().BeEmpty();
        Directory.GetFiles(escenario.Contexto.Procesaria).Should().BeEmpty();
    }

    [Fact]
    public async Task Intento2_Fallo_Intento3_OpenAiExito_Procesados()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("reintento.pdf");
        var txt = escenario.CrearTxtLote();

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FalloBarcode());
        documento.ProcesarConCodigoConocidoAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Exito("KV222222"));

        var openAi = Substitute.For<IOpenAiBarcodeService>();
        openAi.LeerCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.CodigoEncontrado,
                Codigo = "KV222222",
                Documento = Documento("KV222222")
            });

        var servicio = escenario.CrearServicio(documento, openAi);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        await openAi.Received(1).LeerCodigoAsync(
            Arg.Is<string>(r => r.Contains("procesaria")),
            Arg.Any<CancellationToken>());

    }

    [Fact]
    public async Task OpenAi_NoBarcode_VaANoprocesados()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("sin-codigo.pdf");
        var txt = escenario.CrearTxtLote();

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FalloBarcode());

        var openAi = Substitute.For<IOpenAiBarcodeService>();
        openAi.LeerCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new OpenAiBarcodeResult { Tipo = OpenAiBarcodeResultKind.NoBarcode });

        var servicio = escenario.CrearServicio(documento, openAi);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        Directory.GetFiles(escenario.Contexto.Noprocesados).Should().HaveCount(1);
    }

    [Fact]
    public async Task OpenAi_ErrorServicio_NoprocesadosYCorreoUnico()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("openai-falla.pdf");
        var txt = escenario.CrearTxtLote();

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(FalloBarcode());

        var openAi = Substitute.For<IOpenAiBarcodeService>();
        openAi.LeerCodigoAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new OpenAiBarcodeResult
            {
                Tipo = OpenAiBarcodeResultKind.ErrorServicio,
                ErrorMensaje = "timeout"
            });

        var email = Substitute.For<IEmailNotificationService>();

        var servicio = escenario.CrearServicio(documento, openAi, email);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        Directory.GetFiles(escenario.Contexto.Noprocesados).Should().HaveCount(1);

        var contextoEsperado = RutasLoteResolver.Resolver(escenario.Contexto.Procesar);

        await email.Received(1).EnviarFalloOpenAiLoteAsync(
            Arg.Is<RutasLoteContext>(c =>
                c.Noprocesados == contextoEsperado.Noprocesados &&
                c.Usuario == contextoEsperado.Usuario &&
                c.Fecha == contextoEsperado.Fecha),
            Arg.Is<int>(n => n >= 1),
            Arg.Is<string>(m => m.Contains("timeout")),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Limpieza_ConservaArchivosEnNoprocesados()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("fallo-api.pdf");
        escenario.CrearPdfEnProcesar("ok.pdf");
        var txt = escenario.CrearTxtLote();

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        documento.ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var ruta = call.ArgAt<string>(0);
                return ruta.Contains("fallo-api", StringComparison.OrdinalIgnoreCase)
                    ? FalloApi()
                    : Exito();
            });

        var servicio = escenario.CrearServicio(documento);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        Directory.GetFiles(escenario.Contexto.Noprocesados).Should().ContainSingle()
            .Which.Should().EndWith("fallo-api.pdf");
        Directory.GetFiles(escenario.Contexto.Procesados).Should().BeEmpty("solo limpia temporales");
    }

    [Fact]
    public async Task TxtInvalido_ConservaTxtParaRevision()
    {
        using var escenario = new Worker2Escenario();
        var txt = Path.Combine(escenario.ArchivosNuevos, "lote-vacio.txt");
        await File.WriteAllTextAsync(txt, "   " + Environment.NewLine);

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        var servicio = escenario.CrearServicio(documento);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        File.Exists(txt).Should().BeTrue("TXT inválido no debe borrarse");
        await documento.DidNotReceive().ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CarpetasIncompletas_NoProcesaYConservaTxt()
    {
        using var escenario = new Worker2Escenario();
        escenario.CrearPdfEnProcesar("doc.pdf");
        var txt = escenario.CrearTxtLote();

        // Simula carpeta faltante (Worker 1 no la creó)
        Directory.Delete(escenario.Contexto.Procesaria, recursive: true);

        var documento = Substitute.For<IDocumentoProcesamientoService>();
        var servicio = escenario.CrearServicio(documento);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        File.Exists(txt).Should().BeTrue("TXT se conserva si faltan carpetas");
        await documento.DidNotReceive().ProcesarAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }
}
