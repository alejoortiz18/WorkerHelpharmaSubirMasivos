using FluentAssertions;
using Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models.Dto;
using NSubstitute;
using Services;
using Xunit;

namespace Tests.Infrastructure;

/// <summary>
/// Prueba end-to-end REAL del guardado de IdBodega/IdCartera en dbo.DocumentosProcesados.
///
/// Flujo real de punta a punta:
///   PDF real (ArchivosTest) -> IronBarcode real -> API de datos REAL de Helpharma
///   (https://api-soportes.helpharma.com.co .../DatosSoportes con la X-API-KEY de producción)
///   -> LoteProcesamientoService real -> TrazabilidadSqlService real (BD Scaneados).
///
/// IdBodega/IdCartera provienen de la respuesta REAL del API de datos; aquí NO se simulan.
///
/// Único paso omitido a propósito: la subida al API físico (soporte/fisico), porque
/// crearía un soporte físico real en producción y NO aporta IdBodega/IdCartera (esos campos
/// los entrega el API de datos). El documento real se usa para leer su código y consultar
/// sus datos reales.
///
/// Requiere red hacia api-soportes.helpharma.com.co y acceso a la BD Scaneados.
///
/// Ejecutar:
///   dotnet test --filter "FullyQualifiedName~TrazabilidadIdBodegaCarteraE2ETests"
/// </summary>
[Trait("Category", "IntegracionProduccion")]
public class TrazabilidadIdBodegaCarteraE2ETests
{
    [Theory]
    [InlineData("CRC_900277244_FE249758.pdf")]
    public async Task ProcesarPdfReal_GuardaIdBodegaEIdCarteraRealesEnBd(string nombrePdf)
    {
        var rutaPdf = ResolverRutaArchivosTest(nombrePdf);
        File.Exists(rutaPdf).Should().BeTrue($"debe existir el PDF de prueba en {rutaPdf}");

        var usuario = "e2e_idbodega_real";
        var fecha = DateTime.Now.ToString("yyyy-MM-dd");

        // API de datos REAL (misma X-API-KEY que usa producción), sin subida física.
        var soporteReal = new SoporteDatosRealesService(CrearSoporteApiReal());

        var connectionString = new SqlConnectionStringBuilder(
            Worker2IntegracionHelper.ConnectionStringProduccion)
        {
            InitialCatalog = "Scaneados"
        }.ConnectionString;

        await LimpiarRegistroAsync(connectionString, usuario, fecha, nombrePdf);

        var raiz = Path.Combine(Path.GetTempPath(), "e2e-idbodega-real-" + Guid.NewGuid().ToString("N"));
        try
        {
            var (rutaTxt, _) = PrepararLoteLocal(raiz, usuario, fecha, rutaPdf, nombrePdf);

            var servicio = Worker2IntegracionHelper.CrearServicioLote(soporteOverride: soporteReal);

            await servicio.ProcesarLoteAsync(rutaTxt, CancellationToken.None);

            soporteReal.UltimoCodigo.Should().NotBeNullOrWhiteSpace(
                "IronBarcode debe leer el código real del PDF antes de consultar el API");

            soporteReal.UltimosDatos.Should().NotBeNull(
                $"el API de datos REAL debe responder para el soporte {soporteReal.UltimoCodigo}");

            var registro = await LeerRegistroAsync(connectionString, usuario, fecha, nombrePdf);

            registro.Should().NotBeNull("el documento debe haberse registrado en la BD");
            registro!.Value.IdBodega.Should().NotBeNullOrWhiteSpace("IdBodega real no debe quedar vacío");
            registro.Value.IdCartera.Should().NotBeNullOrWhiteSpace("IdCartera real no debe quedar vacío");
            registro.Value.IdBodega.Should().Be(soporteReal.UltimosDatos!.IdBodega);
            registro.Value.IdCartera.Should().Be(soporteReal.UltimosDatos!.IdCartera);
            registro.Value.Procesado.Should().BeTrue();

            // Evidencia visible en la salida del test.
            Console.WriteLine(
                $"[REAL] Pdf={nombrePdf} Soporte={soporteReal.UltimoCodigo} " +
                $"IdBodega={registro.Value.IdBodega} IdCartera={registro.Value.IdCartera} " +
                $"IdPaciente={registro.Value.IdPaciente} Paciente={soporteReal.UltimosDatos!.NombrePaciente}");
        }
        finally
        {
            // Con MASIVOS_E2E_KEEP=1 se conserva la fila en la BD para inspección manual.
            var conservar = string.Equals(
                Environment.GetEnvironmentVariable("MASIVOS_E2E_KEEP"), "1", StringComparison.OrdinalIgnoreCase);
            if (!conservar)
                await LimpiarRegistroAsync(connectionString, usuario, fecha, nombrePdf);

            if (Directory.Exists(raiz))
                Directory.Delete(raiz, recursive: true);
        }
    }

    /// <summary>
    /// Llama al API de datos REAL (DatosSoportes) y devuelve sus datos sin subir al API físico.
    /// </summary>
    private sealed class SoporteDatosRealesService : ISoporteProcesamientoService
    {
        private readonly SoporteApiService _api;

        public SoporteDatosRealesService(SoporteApiService api) => _api = api;

        public string? UltimoCodigo { get; private set; }
        public SoporteResponseDto? UltimosDatos { get; private set; }

        public async Task<SoporteProcesamientoResult> ProcesarAsync(
            string soporte, string rutaArchivoPdf, CancellationToken cancellationToken = default)
        {
            var soporteNormalizado = soporte.Replace("-", string.Empty);
            UltimoCodigo = soporteNormalizado;

            var datos = await _api.EnviarSoporteAsync(soporteNormalizado);
            UltimosDatos = datos;

            if (datos == null)
            {
                return new SoporteProcesamientoResult
                {
                    Estado = SoporteProcesamientoEstado.FalloApiDatos,
                    Soporte = soporteNormalizado
                };
            }

            // Se omite la subida física (soporte/fisico) a propósito; los campos IdBodega/
            // IdCartera ya vienen del API de datos real.
            return new SoporteProcesamientoResult
            {
                Estado = SoporteProcesamientoEstado.Exito,
                Soporte = soporteNormalizado,
                Datos = datos
            };
        }
    }

    private static SoporteApiService CrearSoporteApiReal()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.Configure<ApiCredentialsSettings>(
            Worker2IntegracionHelper.Config.GetSection("ApiCredentials"));
        services.AddHttpClient<SoporteApiService>();

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<SoporteApiService>();
    }

    private static (string RutaTxt, string CarpetaProcesar) PrepararLoteLocal(
        string raiz, string usuario, string fecha, string rutaPdfOrigen, string nombrePdf)
    {
        var carpetaDia = Path.Combine(raiz, usuario, fecha);
        foreach (var sub in new[] { "procesar", "procesando", "procesaria", "noprocesados", "procesados", "error", "log" })
            Directory.CreateDirectory(Path.Combine(carpetaDia, sub));

        var procesar = Path.Combine(carpetaDia, "procesar");
        File.Copy(rutaPdfOrigen, Path.Combine(procesar, nombrePdf), overwrite: true);

        var archivosNuevos = Path.Combine(raiz, "ArchivosNuevos");
        Directory.CreateDirectory(archivosNuevos);
        var rutaTxt = Path.Combine(archivosNuevos, $"{usuario}-{fecha} 08-00-00AM.txt");
        File.WriteAllText(rutaTxt, procesar + Environment.NewLine);

        return (rutaTxt, procesar);
    }

    private static string ResolverRutaArchivosTest(string nombre)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidato = Path.Combine(dir.FullName, "ArchivosTest", nombre);
            if (File.Exists(candidato))
                return candidato;
            dir = dir.Parent;
        }

        throw new FileNotFoundException($"No se encontró ArchivosTest\\{nombre} subiendo desde {AppContext.BaseDirectory}");
    }

    private static async Task<(string? IdBodega, string? IdCartera, int? IdPaciente, bool Procesado)?> LeerRegistroAsync(
        string connectionString, string usuario, string fecha, string nombreArchivo)
    {
        const string sql = """
SELECT TOP 1 dp.IdBodega, dp.IdCartera, dp.IdPaciente, dp.Procesado
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @Usuario AND fp.FechaProcesamiento = @Fecha AND dp.NombreArchivo = @NombreArchivo;
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Usuario", usuario);
        command.Parameters.Add("@Fecha", System.Data.SqlDbType.Date).Value =
            DateOnly.ParseExact(fecha, "yyyy-MM-dd").ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@NombreArchivo", nombreArchivo);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return (
            reader.IsDBNull(0) ? null : reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            !reader.IsDBNull(3) && reader.GetBoolean(3));
    }

    private static async Task LimpiarRegistroAsync(
        string connectionString, string usuario, string fecha, string nombreArchivo)
    {
        const string sql = """
DELETE dp
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @Usuario AND fp.FechaProcesamiento = @Fecha AND dp.NombreArchivo = @NombreArchivo;
""";

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue("@Usuario", usuario);
        command.Parameters.Add("@Fecha", System.Data.SqlDbType.Date).Value =
            DateOnly.ParseExact(fecha, "yyyy-MM-dd").ToDateTime(TimeOnly.MinValue);
        command.Parameters.AddWithValue("@NombreArchivo", nombreArchivo);

        await command.ExecuteNonQueryAsync();
    }
}
