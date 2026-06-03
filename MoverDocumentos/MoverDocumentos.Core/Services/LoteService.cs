using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoverDocumentos.Core.Configuration;

namespace MoverDocumentos.Core.Services;

public class LoteService : IDisposable
{
    private readonly RutasSettings _rutas;
    private readonly LoteSettings _lote;
    private readonly ILogger<LoteService> _logger;
    private readonly object _lock = new();
    private readonly Dictionary<string, LoteActivo> _lotes = new(StringComparer.OrdinalIgnoreCase);

    public LoteService(
        IOptions<RutasSettings> rutasOptions,
        IOptions<LoteSettings> loteOptions,
        ILogger<LoteService> logger)
    {
        _rutas = rutasOptions.Value;
        _lote = loteOptions.Value;
        _logger = logger;
    }

    public void RegistrarMovimiento(string usuario, DateOnly fecha, string rutaProcesar)
    {
        var clave = $"{usuario}|{fecha:yyyy-MM-dd}";

        lock (_lock)
        {
            if (!_lotes.TryGetValue(clave, out var lote))
            {
                lote = new LoteActivo(usuario, fecha, rutaProcesar);
                _lotes[clave] = lote;
            }

            lote.UltimoMovimientoUtc = DateTime.UtcNow;
            lote.HuboNuevosArchivosDesdeUltimoTxt = true;

            lote.TimerInactividad?.Dispose();
            lote.TimerInactividad = new Timer(
                _ => CerrarLote(clave),
                null,
                TimeSpan.FromSeconds(_lote.SegundosInactividadParaCerrarLote),
                Timeout.InfiniteTimeSpan);
        }
    }

    private void CerrarLote(string clave)
    {
        LoteActivo? lote;

        lock (_lock)
        {
            if (!_lotes.TryGetValue(clave, out lote))
                return;

            var inactividad = DateTime.UtcNow - lote.UltimoMovimientoUtc;
            if (inactividad.TotalSeconds < _lote.SegundosInactividadParaCerrarLote - 1)
                return;

            if (!lote.HuboNuevosArchivosDesdeUltimoTxt)
            {
                _lotes.Remove(clave);
                lote.TimerInactividad?.Dispose();
                return;
            }

            _lotes.Remove(clave);
            lote.TimerInactividad?.Dispose();
        }

        try
        {
            Directory.CreateDirectory(_rutas.RutaArchivosNuevos);

            var hora = DateTime.Now.ToString(
                _lote.FormatoHoraEnNombreTxt,
                CultureInfo.InvariantCulture);
            var nombreTxt = $"{lote!.Usuario}-{lote.Fecha:yyyy-MM-dd} {hora}.txt";
            var rutaTxt = Path.Combine(_rutas.RutaArchivosNuevos, nombreTxt);

            var rutaProcesarTxt = RutasSettings.NormalizarRutaRed(lote.RutaProcesar);
            File.WriteAllText(rutaTxt, rutaProcesarTxt + Environment.NewLine);

            lock (_lock)
            {
                lote.HuboNuevosArchivosDesdeUltimoTxt = false;
            }

            _logger.LogInformation(
                "LoteCerrado | Txt={Txt} | RutaProcesar={RutaProcesar}",
                rutaTxt,
                rutaProcesarTxt);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "ErrorCerrarLote | Usuario={Usuario} | Fecha={Fecha}",
                lote!.Usuario,
                lote.Fecha);
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var lote in _lotes.Values)
                lote.TimerInactividad?.Dispose();
            _lotes.Clear();
        }
    }

    private sealed class LoteActivo
    {
        public LoteActivo(string usuario, DateOnly fecha, string rutaProcesar)
        {
            Usuario = usuario;
            Fecha = fecha;
            RutaProcesar = rutaProcesar;
        }

        public string Usuario { get; }
        public DateOnly Fecha { get; }
        public string RutaProcesar { get; }
        public DateTime UltimoMovimientoUtc { get; set; } = DateTime.UtcNow;
        public bool HuboNuevosArchivosDesdeUltimoTxt { get; set; } = true;
        public Timer? TimerInactividad { get; set; }
    }
}
