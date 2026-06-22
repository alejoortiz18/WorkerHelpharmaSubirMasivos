using GestionArchivosEscaneados.Models.Entities;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Options;

namespace GestionArchivosEscaneados.Infrastructure.Unc;

public static class RutasDiaHelper
{
    private static readonly System.Text.RegularExpressions.Regex FechaValida =
        new(@"^\d{4}-\d{2}-\d{2}$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public static bool EsFechaValida(string fecha) => FechaValida.IsMatch(fecha);

    public static RutasDiaContext Resolver(RutasSettings rutas, string usuario, string fecha)
    {
        if (!EsFechaValida(fecha))
            throw new ArgumentException($"Formato de fecha inválido: {fecha}", nameof(fecha));

        var carpetaDia = Path.Combine(rutas.CarpetaUsuario(usuario), fecha);

        return new RutasDiaContext
        {
            Usuario = usuario,
            Fecha = fecha,
            Procesar = Path.Combine(carpetaDia, "procesar"),
            Noprocesados = Path.Combine(carpetaDia, "noprocesados"),
            Procesados = Path.Combine(carpetaDia, "procesados"),
            Log = Path.Combine(carpetaDia, "log")
        };
    }
}

public class UncStorageService
{
    private readonly RutasSettings _rutas;
    private readonly UncConexionService _uncConexion;

    public UncStorageService(IOptions<RutasSettings> rutas, UncConexionService uncConexion)
    {
        _rutas = rutas.Value;
        _uncConexion = uncConexion;
    }

    public RutasDiaContext ObtenerRutasDia(string usuario, string fecha) =>
        RutasDiaHelper.Resolver(_rutas, usuario, fecha);

    public IReadOnlyList<ArchivoNoProcesado> ListarNoProcesados(string usuario, string fecha) =>
        _uncConexion.EjecutarConAcceso(() =>
        {
            var rutas = ObtenerRutasDia(usuario, fecha);
            if (!Directory.Exists(rutas.Noprocesados))
                return (IReadOnlyList<ArchivoNoProcesado>)[];

            return Directory.GetFiles(rutas.Noprocesados, "*.pdf")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Select(f => new ArchivoNoProcesado
                {
                    NombreArchivo = Path.GetFileName(f),
                    Fecha = fecha,
                    RutaCompleta = f,
                    TieneIntentoPrevio = File.Exists(ObtenerRutaIntentoPrevio(f))
                })
                .ToList();
        });

    public string? ResolverRutaPdfSegura(string usuario, string fecha, string nombreArchivo) =>
        _uncConexion.EjecutarConAcceso(() =>
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo) ||
                nombreArchivo.Contains("..") ||
                nombreArchivo.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;

            var rutas = ObtenerRutasDia(usuario, fecha);
            var ruta = Path.Combine(rutas.Noprocesados, nombreArchivo);
            return File.Exists(ruta) ? ruta : null;
        });

    public byte[]? LeerPdfNoProcesado(string usuario, string fecha, string nombreArchivo) =>
        _uncConexion.EjecutarConAcceso(() =>
        {
            var ruta = ResolverRutaPdfSeguraInterno(usuario, fecha, nombreArchivo);
            return ruta == null ? null : File.ReadAllBytes(ruta);
        });

    public void MoverANoprocesadosAProcesados(string rutaOrigen, RutasDiaContext rutas) =>
        _uncConexion.EjecutarConAcceso(() =>
        {
            Directory.CreateDirectory(rutas.Procesados);

            var nombre = Path.GetFileName(rutaOrigen);
            var destino = Path.Combine(rutas.Procesados, nombre);
            var rutaNoprocesados = Path.Combine(rutas.Noprocesados, nombre);

            if (File.Exists(destino))
                File.Delete(destino);

            var origen = File.Exists(rutaOrigen)
                ? rutaOrigen
                : File.Exists(rutaNoprocesados)
                    ? rutaNoprocesados
                    : null;

            if (origen == null)
                return;

            try
            {
                File.Move(origen, destino);
            }
            catch (IOException)
            {
                var bytes = File.ReadAllBytes(origen);
                File.WriteAllBytes(destino, bytes);
                EliminarArchivoConReintentos(origen);
            }

            if (File.Exists(rutaNoprocesados))
                EliminarArchivoConReintentos(rutaNoprocesados);
        });

    private static void EliminarArchivoConReintentos(string ruta, int intentos = 8, int esperaMs = 250)
    {
        for (var i = 0; i < intentos; i++)
        {
            if (!File.Exists(ruta))
                return;

            try
            {
                File.Delete(ruta);
                return;
            }
            catch (IOException) when (i < intentos - 1)
            {
                Thread.Sleep(esperaMs * (i + 1));
            }
        }

        if (File.Exists(ruta))
            throw new IOException($"No se pudo eliminar el archivo de noprocesados: {ruta}");
    }

    public bool EliminarPdfNoProcesado(string usuario, string fecha, string nombreArchivo) =>
        _uncConexion.EjecutarConAcceso(() =>
        {
            var ruta = ResolverRutaPdfSeguraInterno(usuario, fecha, nombreArchivo);
            if (ruta == null)
                return false;

            EliminarMarcadorIntentoPrevio(ruta);
            File.Delete(ruta);
            return true;
        });

    public bool ExisteIntentoPrevio(string usuario, string fecha, string nombreArchivo) =>
        _uncConexion.EjecutarConAcceso(() =>
        {
            var ruta = ResolverRutaPdfSeguraInterno(usuario, fecha, nombreArchivo);
            return ruta != null && File.Exists(ObtenerRutaIntentoPrevio(ruta));
        });

    public void MarcarIntentoPrevio(string usuario, string fecha, string nombreArchivo) =>
        _uncConexion.EjecutarConAcceso(() =>
        {
            var ruta = ResolverRutaPdfSeguraInterno(usuario, fecha, nombreArchivo);
            if (ruta == null)
                return;

            var marcador = ObtenerRutaIntentoPrevio(ruta);
            Directory.CreateDirectory(Path.GetDirectoryName(marcador)!);
            File.WriteAllText(marcador, DateTime.UtcNow.ToString("O"));
        });

    public void EliminarMarcadorIntentoPrevio(string usuario, string fecha, string nombreArchivo) =>
        _uncConexion.EjecutarConAcceso(() =>
        {
            var ruta = ResolverRutaPdfSeguraInterno(usuario, fecha, nombreArchivo);
            if (ruta == null)
                return;

            EliminarMarcadorIntentoPrevio(ruta);
        });

    private string? ResolverRutaPdfSeguraInterno(string usuario, string fecha, string nombreArchivo)
    {
        if (string.IsNullOrWhiteSpace(nombreArchivo) ||
            nombreArchivo.Contains("..") ||
            nombreArchivo.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            return null;

        var rutas = ObtenerRutasDia(usuario, fecha);
        var ruta = Path.Combine(rutas.Noprocesados, nombreArchivo);
        return File.Exists(ruta) ? ruta : null;
    }

    private static string ObtenerRutaIntentoPrevio(string rutaPdf) =>
        Path.Combine(Path.GetDirectoryName(rutaPdf)!, Path.GetFileName(rutaPdf) + ".attempt");

    private static void EliminarMarcadorIntentoPrevio(string rutaPdf)
    {
        var marcador = ObtenerRutaIntentoPrevio(rutaPdf);
        if (File.Exists(marcador))
            File.Delete(marcador);
    }
}
