using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Models.Dto;
using NSubstitute;
using Services;
using Xunit;

namespace Tests.Infrastructure;

/// <summary>
/// Pruebas de integración tipo producción:
/// - PDFs reales (.github/DocumentosTest)
/// - IronBarcode real (licencia appsettings.json)
/// - Flujo completo LoteProcesamientoService
///
/// Ejecutar:
///   dotnet test --filter "Category=IntegracionProduccion"
///
/// Con APIs Helpharma reales (red + credenciales válidas):
///   set MASIVOS_E2E_API=1
///   dotnet test --filter "Category=IntegracionProduccion"
///
/// Rutas en UNC de producción (appsettings Rutas:RaizUnc o MASIVOS_UNC_RAIZ):
///   set MASIVOS_USUARIO_PRUEBA=alejandro.ortiz
///   set MASIVOS_FECHA_PRUEBA=2026-06-03
///   dotnet test --filter "Category=IntegracionProduccion"
///
/// OpenAI real (ApiKey por variable, no en git):
///   set OpenAi__ApiKey=sk-proj-...
///   set MASIVOS_E2E_OPENAI=1
///   dotnet test --filter "OpenAiReal"
///
/// Correo SMTP real tras fallo OpenAI en flujo completo de lote (ruta derivada del TXT, no quemada):
///   set Email__SmtpHost=smtp.office365.com
///   set Email__Usuario=...
///   set Email__Clave=...
///   set OpenAi__ApiKey=sk-proj-...
///   set MASIVOS_E2E_EMAIL=1
///   set MASIVOS_E2E_OPENAI=1
///   dotnet test --filter "LoteCompleto_EnUnc_OpenAiFallo_EnviaCorreoConRutaDelLote"
/// </summary>
[Trait("Category", "IntegracionProduccion")]
public class Worker2IntegracionProduccionTests
{
    private const string PdfConBarcode = "CRC_900277244_KV_351697.pdf";

    [Fact]
    public void BarcodeReal_LeeCodigoEnPdfDePrueba()
    {
        Worker2IntegracionHelper.InicializarLicenciaIronBarcode();

        var rutaPdf = Path.Combine(Worker2IntegracionHelper.RutaDocumentosTest, PdfConBarcode);
        File.Exists(rutaPdf).Should().BeTrue($"debe existir el PDF de prueba en {rutaPdf}");

        var barcode = new BarcodeRegionService(NullLogger<BarcodeRegionService>.Instance);
        var resultado = barcode.ProcesarPdf(rutaPdf);

        resultado.Should().NotBeNull("IronBarcode debe leer un código en un PDF real de DocumentosTest");
        $"{resultado!.Prefijo}{resultado.Numero}".Should().MatchRegex("^[A-Z]+\\d+$");
    }

    [Fact]
    public async Task LoteCompleto_PdfReal_BarcodeReal_ApiSimulada_ProcesaYLimpia()
    {
        if (!Worker2IntegracionHelper.UncProduccionDisponible)
            return;

        var soporteMock = Substitute.For<ISoporteProcesamientoService>();
        soporteMock
            .ProcesarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.Exito,
                Soporte = call.ArgAt<string>(0)
            }));

        using var escenario = Worker2IntegracionHelper.CrearEscenarioProduccion();
        Worker2IntegracionHelper.CopiarPdfPrueba(PdfConBarcode, escenario.Contexto.Procesar, escenario);
        var txt = escenario.CrearTxtLote();

        var logDiario = new LogDiarioService(NullLogger<LogDiarioService>.Instance);
        var antes = await logDiario.LeerContadoresAsync(escenario.Contexto.RutaLogDiario, CancellationToken.None);

        var servicio = Worker2IntegracionHelper.CrearServicioLote(soporteOverride: soporteMock);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        File.Exists(txt).Should().BeFalse("el TXT del lote debe eliminarse al cerrar");
        File.Exists(Path.Combine(escenario.Contexto.Procesar, PdfConBarcode)).Should().BeFalse();
        File.Exists(Path.Combine(escenario.Contexto.Noprocesados, PdfConBarcode)).Should().BeFalse(
            "el PDF de prueba debe procesarse, no quedar en noprocesados");

        var despues = await logDiario.LeerContadoresAsync(escenario.Contexto.RutaLogDiario, CancellationToken.None);
        (despues.Procesados - antes.Procesados).Should().Be(1);

        await soporteMock.Received(1).ProcesarAsync(
            Arg.Is<string>(s => s.Length > 0 && char.IsLetter(s[0])),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoteCompleto_TresPdfsReales_BarcodeReal_ProcesaTanda()
    {
        if (!Worker2IntegracionHelper.UncProduccionDisponible)
            return;

        var soporteMock = Substitute.For<ISoporteProcesamientoService>();
        soporteMock
            .ProcesarAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.Exito,
                Soporte = call.ArgAt<string>(0)
            }));

        var pdfs = new[]
        {
            "CRC_900277244_KV_351697.pdf",
            "CRC_900277244_KV_352285.pdf",
            "CRC_900277244_KV_353381.pdf"
        };

        using var escenario = Worker2IntegracionHelper.CrearEscenarioProduccion();
        foreach (var pdf in pdfs)
            Worker2IntegracionHelper.CopiarPdfPrueba(pdf, escenario.Contexto.Procesar, escenario);

        var txt = escenario.CrearTxtLote();
        var logDiario = new LogDiarioService(NullLogger<LogDiarioService>.Instance);
        var antes = await logDiario.LeerContadoresAsync(escenario.Contexto.RutaLogDiario, CancellationToken.None);

        var servicio = Worker2IntegracionHelper.CrearServicioLote(soporteOverride: soporteMock);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        escenario.Contexto.Procesar.Should().StartWith(@"\\");
        foreach (var pdf in pdfs)
        {
            File.Exists(Path.Combine(escenario.Contexto.Procesar, pdf)).Should().BeFalse();
            File.Exists(Path.Combine(escenario.Contexto.Noprocesados, pdf)).Should().BeFalse();
        }

        var despues = await logDiario.LeerContadoresAsync(escenario.Contexto.RutaLogDiario, CancellationToken.None);
        (despues.Procesados - antes.Procesados).Should().Be(3);
        await soporteMock.Received(3).ProcesarAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoteCompleto_ApiReal_SoloSiVariableEntorno()
    {
        if (!Worker2IntegracionHelper.UsarApisReales || !Worker2IntegracionHelper.UncProduccionDisponible)
        {
            // Permite tener el test en el proyecto sin fallar en CI/local sin red API
            return;
        }

        using var escenario = Worker2IntegracionHelper.CrearEscenarioProduccion();
        Worker2IntegracionHelper.CopiarPdfPrueba(PdfConBarcode, escenario.Contexto.Procesar, escenario);
        var txt = escenario.CrearTxtLote();

        var logDiario = new LogDiarioService(NullLogger<LogDiarioService>.Instance);
        var antes = await logDiario.LeerContadoresAsync(escenario.Contexto.RutaLogDiario, CancellationToken.None);

        var servicio = Worker2IntegracionHelper.CrearServicioLote();

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        var despues = await logDiario.LeerContadoresAsync(escenario.Contexto.RutaLogDiario, CancellationToken.None);
        (despues.Procesados - antes.Procesados).Should().Be(1);
    }

    [Fact]
    public async Task OpenAiReal_LeeCodigoEnPdfDePrueba()
    {
        if (!Worker2IntegracionHelper.UsarOpenAiReal)
            return;

        Worker2IntegracionHelper.InicializarLicenciaIronBarcode();

        var rutaPdf = Path.Combine(Worker2IntegracionHelper.RutaDocumentosTest, PdfConBarcode);
        var openAi = Worker2IntegracionHelper.CrearOpenAiServicio();

        var resultado = await openAi.LeerCodigoAsync(rutaPdf, CancellationToken.None);

        if (resultado.Tipo == OpenAiBarcodeResultKind.ErrorServicio &&
            resultado.ErrorMensaje?.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) == true)
        {
            // La clave es válida y la API respondió; cuenta sin saldo (esperado en pruebas sin pago).
            return;
        }

        resultado.Tipo.Should().NotBe(
            OpenAiBarcodeResultKind.ErrorServicio,
            $"OpenAI debe responder; error: {resultado.ErrorMensaje}");
    }

    [Fact]
    public async Task OpenAiReal_Diagnostico_ImprimeRespuesta()
    {
        if (!Worker2IntegracionHelper.UsarOpenAiReal)
            return;

        Worker2IntegracionHelper.InicializarLicenciaIronBarcode();

        var openAi = Worker2IntegracionHelper.CrearOpenAiServicio();
        var pdfs = new[]
        {
            Path.Combine(Worker2IntegracionHelper.RutaDocumentosTest, PdfConBarcode),
            Path.Combine(Worker2IntegracionHelper.RutaDocumentosTest, "blank-sin-barcode.pdf")
        };

        foreach (var rutaPdf in pdfs)
        {
            var resultado = await openAi.LeerCodigoAsync(rutaPdf, CancellationToken.None);
            Console.WriteLine(
                "OPENAI_DIAG | Archivo={0} | Tipo={1} | Codigo={2} | Error={3}",
                Path.GetFileName(rutaPdf),
                resultado.Tipo,
                resultado.Codigo ?? "(null)",
                resultado.ErrorMensaje ?? "(null)");
        }
    }

    [Fact]
    public async Task LoteCompleto_EnUnc_OpenAiFallo_EnviaCorreoConRutaDelLote()
    {
        if (!Worker2IntegracionHelper.UsarEmailReal ||
            !Worker2IntegracionHelper.UsarOpenAiReal ||
            !Worker2IntegracionHelper.UncProduccionDisponible)
            return;

        using var escenario = Worker2IntegracionHelper.CrearEscenarioProduccion();
        const string nombrePdf = "sin-barcode-prueba-openai.pdf";
        escenario.CrearPdfLegibleSinBarcode(nombrePdf);
        var txt = escenario.CrearTxtLote();

        var rutaProcesarTxt = (await File.ReadAllLinesAsync(txt)).FirstOrDefault()?.Trim();
        rutaProcesarTxt.Should().NotBeNullOrWhiteSpace();
        var contextoDesdeTxt = RutasLoteResolver.Resolver(rutaProcesarTxt!);
        var rutaNoprocesadosEsperada = Worker2IntegracionHelper.NormalizarRutaUnc(
            contextoDesdeTxt.Noprocesados);

        var captura = new EmailNotificacionCaptura(Worker2IntegracionHelper.CrearEmailReal());
        var servicio = Worker2IntegracionHelper.CrearServicioLote(emailOverride: captura);

        await servicio.ProcesarLoteAsync(txt, CancellationToken.None);

        captura.Invocaciones.Should().Be(1,
            "solo debe enviarse 1 correo por lote cuando OpenAI falla al subir/procesar");
        captura.UltimoContexto.Should().NotBeNull();
        captura.UltimaCantidadArchivos.Should().BeGreaterThan(0);

        var rutaEnCorreo = Worker2IntegracionHelper.NormalizarRutaUnc(
            captura.UltimoContexto!.Noprocesados);

        rutaEnCorreo.Should().Be(rutaNoprocesadosEsperada,
            "la ruta del correo debe derivarse del TXT del lote vía RutasLoteResolver, no estar quemada");
        rutaEnCorreo.Should().StartWith(@"\\");
        rutaEnCorreo.Should().Contain(Worker2IntegracionHelper.UsuarioPrueba);
        rutaEnCorreo.Should().EndWith(@"\noprocesados");

        File.Exists(Path.Combine(rutaEnCorreo, nombrePdf)).Should().BeTrue(
            "el PDF afectado por el fallo OpenAI debe quedar en la misma carpeta noprocesados del correo");

        captura.UltimoErrorMensaje.Should().NotBeNullOrWhiteSpace(
            "el correo debe incluir el error real devuelto por OpenAI");
    }

    [Fact]
    public async Task WatcherReal_ArchivosNuevos_ProcesaTxtYRegistraEnSql()
    {
        if (!Worker2IntegracionHelper.UncProduccionDisponible)
            return;

        await Worker2IntegracionHelper.EnsureTrazabilidadSchemaAsync();

        var usuario = $"masivos.e2e.{Guid.NewGuid():N}"[..28];
        var fecha = DateTime.Now.ToString("yyyy-MM-dd");
        using var escenario = new Worker2EscenarioProduccion(
            Worker2IntegracionHelper.RaizUncProduccion,
            usuario,
            fecha);

        var nombreArchivo = $"e2e-{Guid.NewGuid():N}.pdf";
        var origen = Path.Combine(Worker2IntegracionHelper.RutaDocumentosTest, PdfConBarcode);
        var destino = Path.Combine(escenario.Contexto.Procesar, nombreArchivo);
        File.Copy(origen, destino, overwrite: true);
        escenario.RegistrarArchivoDePrueba(destino);

        var lote = Worker2IntegracionHelper.CrearServicioLote();
        var watcher = Worker2IntegracionHelper.CrearWatcherReal(lote);

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var watcherTask = watcher.EjecutarEscuchaAsync(cts.Token);

        var txt = escenario.CrearTxtLote($"000-e2e-{Guid.NewGuid():N}.txt");

        await EsperarHastaAsync(
            () => !File.Exists(txt),
            TimeSpan.FromMinutes(2),
            cts.Token);

        cts.Cancel();

        try
        {
            await watcherTask;
        }
        catch (OperationCanceledException)
        {
            // cierre esperado del watcher al finalizar la prueba
        }

        var registros = await Worker2IntegracionHelper.ContarRegistrosTrazabilidadAsync(
            usuario,
            fecha,
            nombreArchivo,
            CancellationToken.None);

        File.Exists(txt).Should().BeFalse("el watcher real debe consumir el TXT desde ArchivosNuevos");
        registros.Should().BeGreaterThan(0, "el flujo real debe registrar al menos una fila en SQL Server");
    }

    [Fact]
    public async Task TrazabilidadReal_MismoArchivo_SeConsolidaEnUnaSolaFila()
    {
        await Worker2IntegracionHelper.EnsureTrazabilidadSchemaAsync();

        var usuario = $"masivos.sql.{Guid.NewGuid():N}"[..28];
        var fecha = DateTime.Now.ToString("yyyy-MM-dd");
        const string nombreArchivo = "FPE51023.pdf";

        var contexto = new RutasLoteContext
        {
            Usuario = usuario,
            Fecha = fecha,
            Procesar = @"C:\temp\procesar",
            Procesando = @"C:\temp\procesando",
            Error = @"C:\temp\error",
            Procesaria = @"C:\temp\procesaria",
            Noprocesados = @"C:\temp\noprocesados",
            Procesados = @"C:\temp\procesados",
            Log = @"C:\temp\log"
        };

        var trazabilidad = new TrazabilidadSqlService(
            Microsoft.Extensions.Options.Options.Create(
                Worker2IntegracionHelper.Config.GetSection("TrazabilidadSql").Get<TrazabilidadSqlSettings>()
                ?? new TrazabilidadSqlSettings()),
            NullLogger<TrazabilidadSqlService>.Instance);

        await trazabilidad.RegistrarDocumentoAsync(
            contexto,
            nombreArchivo,
            soporte: null,
            idPaciente: null,
            idBodega: null,
            idCartera: null,
            fechaFactura: null,
            procesado: false,
            CancellationToken.None);

        await trazabilidad.RegistrarDocumentoAsync(
            contexto,
            nombreArchivo,
            soporte: "FCA62993",
            idPaciente: 30233836,
            idBodega: "12",
            idCartera: "34",
            fechaFactura: new DateTime(2026, 6, 4),
            procesado: true,
            CancellationToken.None);

        var resumen = await Worker2IntegracionHelper.LeerResumenTrazabilidadAsync(
            usuario,
            fecha,
            nombreArchivo,
            CancellationToken.None);

        resumen.Cantidad.Should().Be(1, "un mismo archivo del mismo usuario/fecha no debe duplicarse");
        resumen.Soporte.Should().Be("FCA62993");
        resumen.IdPaciente.Should().Be(30233836);
        resumen.Procesado.Should().BeTrue();
    }

    private static async Task EsperarHastaAsync(
        Func<bool> condicion,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var inicio = DateTime.UtcNow;

        while (!condicion())
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (DateTime.UtcNow - inicio > timeout)
                throw new TimeoutException("La condición esperada no se cumplió dentro del tiempo límite.");

            await Task.Delay(1000, cancellationToken);
        }
    }
}

/// <summary>
/// Escenario en UNC de producción; simula carpetas ya creadas por Worker 1.
/// </summary>
public sealed class Worker2EscenarioProduccion : IDisposable
{
    public string Raiz { get; }
    public string ArchivosNuevos { get; }
    public RutasLoteContext Contexto { get; }
    private readonly bool _esUncProduccion;
    private readonly List<string> _archivosCreados = [];
    private readonly List<string> _txtsCreados = [];

    public Worker2EscenarioProduccion(
        string raizUnc,
        string usuario,
        string fecha)
    {
        Raiz = Worker2IntegracionHelper.NormalizarRutaUnc(raizUnc);
        _esUncProduccion = Raiz.StartsWith(@"\\");

        ArchivosNuevos = Worker2IntegracionHelper.NormalizarRutaUnc(
            Path.Combine(Raiz, "ArchivosNuevos"));
        var carpetaDia = Worker2IntegracionHelper.NormalizarRutaUnc(
            Path.Combine(Raiz, usuario, fecha));

        Contexto = new RutasLoteContext
        {
            Usuario = usuario,
            Fecha = fecha,
            Procesar = Worker2IntegracionHelper.NormalizarRutaUnc(Path.Combine(carpetaDia, "procesar")),
            Procesando = Worker2IntegracionHelper.NormalizarRutaUnc(Path.Combine(carpetaDia, "procesando")),
            Error = Worker2IntegracionHelper.NormalizarRutaUnc(Path.Combine(carpetaDia, "error")),
            Procesaria = Worker2IntegracionHelper.NormalizarRutaUnc(Path.Combine(carpetaDia, "procesaria")),
            Noprocesados = Worker2IntegracionHelper.NormalizarRutaUnc(Path.Combine(carpetaDia, "noprocesados")),
            Procesados = Worker2IntegracionHelper.NormalizarRutaUnc(Path.Combine(carpetaDia, "procesados")),
            Log = Worker2IntegracionHelper.NormalizarRutaUnc(Path.Combine(carpetaDia, "log"))
        };

        Directory.CreateDirectory(ArchivosNuevos);
        foreach (var carpeta in Contexto.CarpetasOperativas)
            Directory.CreateDirectory(carpeta);
    }

    public string CrearTxtLote(string? nombreTxt = null)
    {
        nombreTxt ??= $"{Contexto.Usuario}-{Contexto.Fecha} 08-42-51AM.txt";
        var rutaTxt = Path.Combine(ArchivosNuevos, nombreTxt);
        File.WriteAllText(rutaTxt, Contexto.Procesar + Environment.NewLine);
        _txtsCreados.Add(rutaTxt);
        return rutaTxt;
    }

    public void RegistrarArchivoDePrueba(string ruta) => _archivosCreados.Add(ruta);

    /// <summary>PDF válido sin código de barras: llega a procesaria e intento OpenAI.</summary>
    public string CrearPdfLegibleSinBarcode(string nombre)
    {
        var destino = Worker2IntegracionHelper.CopiarPdfPrueba(
            "blank-sin-barcode.pdf",
            Contexto.Procesar,
            this);
        if (!string.Equals(Path.GetFileName(destino), nombre, StringComparison.OrdinalIgnoreCase))
        {
            var renombrado = Path.Combine(Contexto.Procesar, nombre);
            if (File.Exists(renombrado))
                File.Delete(renombrado);
            File.Move(destino, renombrado);
            _archivosCreados.Remove(destino);
            _archivosCreados.Add(renombrado);
            return renombrado;
        }

        return destino;
    }

    /// <summary>PDF mínimo inválido — solo para casos que no requieren intento OpenAI.</summary>
    public string CrearPdfSinBarcode(string nombre)
    {
        var ruta = Path.Combine(Contexto.Procesar, nombre);
        File.WriteAllBytes(ruta, [0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x0A]); // %PDF-1.
        _archivosCreados.Add(ruta);
        return ruta;
    }

    public void Dispose()
    {
        foreach (var ruta in _archivosCreados.Concat(_txtsCreados))
        {
            try
            {
                if (File.Exists(ruta))
                    File.Delete(ruta);
            }
            catch
            {
                // En UNC de producción no fallar el dispose por archivos bloqueados.
            }
        }

        foreach (var nombre in _archivosCreados.Select(Path.GetFileName).Where(n => n != null))
        {
            foreach (var carpeta in Contexto.CarpetasOperativas)
            {
                var candidato = Path.Combine(carpeta, nombre!);
                try
                {
                    if (File.Exists(candidato))
                        File.Delete(candidato);
                }
                catch
                {
                    // Ignorar archivos movidos por el worker a otras carpetas.
                }
            }
        }

        if (!_esUncProduccion && Directory.Exists(Raiz))
            Directory.Delete(Raiz, recursive: true);
    }
}
