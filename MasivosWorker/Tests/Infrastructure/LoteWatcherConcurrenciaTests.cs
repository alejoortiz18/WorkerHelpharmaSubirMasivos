using System.Collections.Concurrent;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;
using Xunit;

namespace Tests.Infrastructure;

/// <summary>
/// Verifica la regla principal del procesamiento concurrente: hasta
/// MaxProcesosSimultaneos lotes en paralelo, pero jamás dos archivos del
/// mismo usuario al mismo tiempo (RF-02 a RF-05).
/// </summary>
public sealed class LoteWatcherConcurrenciaTests : IDisposable
{
    private readonly string _raiz;
    private readonly string _archivosNuevos;

    public LoteWatcherConcurrenciaTests()
    {
        _raiz = Path.Combine(Path.GetTempPath(), "watcher-conc-" + Guid.NewGuid().ToString("N"));
        _archivosNuevos = Path.Combine(_raiz, "ArchivosNuevos");
        Directory.CreateDirectory(_archivosNuevos);
    }

    [Fact]
    public async Task DosHilos_NuncaProcesaDosArchivosDelMismoUsuarioALaVez()
    {
        // Estado inicial del ejemplo del enunciado.
        CrearTxt("usuariopik-2026-06-09 09-27-15AM.txt");
        CrearTxt("dgutierrez-2026-06-09 07-29-15AM.txt");
        CrearTxt("usuariopik-2026-06-09 09-28-15AM.txt");
        CrearTxt("alejandro.ortiz-2026-06-09 09-29-15AM.txt");

        var procesamiento = new ProcesamientoEspia(retardoMs: 120);
        var watcher = CrearWatcher(procesamiento, maxProcesosSimultaneos: 2);

        using var cts = new CancellationTokenSource();
        var ejecucion = watcher.EjecutarEscuchaAsync(cts.Token);

        await EsperarHasta(
            () => procesamiento.Procesados.Count >= 4,
            TimeSpan.FromSeconds(10));

        cts.Cancel();
        await ejecucion;

        procesamiento.ViolacionMismoUsuario.Should()
            .BeFalse("nunca deben ejecutarse dos archivos del mismo usuario a la vez");

        procesamiento.MaxConcurrentesGlobal.Should()
            .BeLessThanOrEqualTo(2, "no se debe superar MaxProcesosSimultaneos");

        procesamiento.MaxConcurrentesGlobal.Should()
            .BeGreaterThanOrEqualTo(2, "se deben procesar usuarios distintos en paralelo");

        procesamiento.Procesados.Should().HaveCount(4);
        procesamiento.Procesados.Should().Contain(new[]
        {
            "usuariopik-2026-06-09 09-27-15AM.txt",
            "dgutierrez-2026-06-09 07-29-15AM.txt",
            "usuariopik-2026-06-09 09-28-15AM.txt",
            "alejandro.ortiz-2026-06-09 09-29-15AM.txt"
        });

        Directory.GetFiles(_archivosNuevos, "*.txt").Should()
            .BeEmpty("todos los lotes completados se eliminan");
    }

    private string CrearTxt(string nombre)
    {
        var ruta = Path.Combine(_archivosNuevos, nombre);
        File.WriteAllText(ruta, Path.Combine(_raiz, "procesar") + Environment.NewLine);
        return ruta;
    }

    private LoteWatcherInfrastructure CrearWatcher(
        ILoteProcesamientoService procesamiento,
        int maxProcesosSimultaneos)
    {
        var rutas = Options.Create(new RutasSettings
        {
            RaizUnc = _raiz,
            ArchivosNuevos = "ArchivosNuevos"
        });

        var fileSettings = Options.Create(new FileSettings
        {
            MaxProcesosSimultaneos = maxProcesosSimultaneos,
            ArchivosNuevosEscaneoSegundos = 1
        });

        var redDisponible = new RedDisponibleService(
            rutas,
            Options.Create(new RedSettings { UsarCredencialesConfiguradas = false }),
            NullLogger<RedDisponibleService>.Instance);

        var registro = new RegistroUsuariosEnProcesoService(
            NullLogger<RegistroUsuariosEnProcesoService>.Instance);

        return new LoteWatcherInfrastructure(
            rutas,
            fileSettings,
            redDisponible,
            procesamiento,
            registro,
            NullLogger<LoteWatcherInfrastructure>.Instance);
    }

    private static async Task EsperarHasta(Func<bool> condicion, TimeSpan limite)
    {
        var fin = DateTime.UtcNow + limite;
        while (DateTime.UtcNow < fin)
        {
            if (condicion())
                return;
            await Task.Delay(25);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
            Directory.Delete(_raiz, recursive: true);
    }

    /// <summary>
    /// Doble de prueba que registra la concurrencia observada por usuario y global,
    /// simula trabajo con un retardo y elimina el TXT para representar el lote completado.
    /// </summary>
    private sealed class ProcesamientoEspia : ILoteProcesamientoService
    {
        private readonly int _retardoMs;
        private readonly object _lock = new();
        private readonly Dictionary<string, int> _activosPorUsuario = new(StringComparer.OrdinalIgnoreCase);
        private int _activosGlobal;

        public ProcesamientoEspia(int retardoMs)
        {
            _retardoMs = retardoMs;
        }

        public ConcurrentBag<string> Procesados { get; } = new();

        public bool ViolacionMismoUsuario { get; private set; }

        public int MaxConcurrentesGlobal { get; private set; }

        public async Task<LoteProcesamientoOutcome> ProcesarLoteAsync(
            string rutaTxt,
            CancellationToken cancellationToken)
        {
            var usuario = UsuarioArchivoResolver.Resolver(rutaTxt);

            lock (_lock)
            {
                _activosGlobal++;
                MaxConcurrentesGlobal = Math.Max(MaxConcurrentesGlobal, _activosGlobal);

                var actuales = _activosPorUsuario.GetValueOrDefault(usuario) + 1;
                _activosPorUsuario[usuario] = actuales;
                if (actuales > 1)
                    ViolacionMismoUsuario = true;
            }

            try
            {
                await Task.Delay(_retardoMs, cancellationToken);
            }
            finally
            {
                lock (_lock)
                {
                    _activosGlobal--;
                    _activosPorUsuario[usuario]--;
                }
            }

            Procesados.Add(Path.GetFileName(rutaTxt));

            if (File.Exists(rutaTxt))
                File.Delete(rutaTxt);

            return LoteProcesamientoOutcome.Completado(1, 0);
        }
    }
}
