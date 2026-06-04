using FluentAssertions;
using Infrastructure;
using Models.Dto;
using Xunit;

namespace Tests.Infrastructure;

public class RutasLoteResolverTests
{
    [Fact]
    public void Resolver_DesdeRutaProcesar_DerivaRutasHermanas()
    {
        const string procesar =
            @"\\192.168.0.69\ArchivosScaneados\alejandro.ortiz\2026-06-03\procesar";

        var ctx = RutasLoteResolver.Resolver(procesar);

        ctx.Usuario.Should().Be("alejandro.ortiz");
        ctx.Fecha.Should().Be("2026-06-03");
        ctx.Procesar.Should().EndWith(@"\procesar");
        ctx.Procesando.Should().EndWith(@"\procesando");
        ctx.Error.Should().EndWith(@"\error");
        ctx.Procesaria.Should().EndWith(@"\procesaria");
        ctx.Noprocesados.Should().EndWith(@"\noprocesados");
        ctx.Procesados.Should().EndWith(@"\procesados");
        ctx.Log.Should().EndWith(@"\log");
        ctx.RutaLogDiario.Should().EndWith(@"2026-06-03\log\2026-06-03.txt");
    }

    [Fact]
    public void Resolver_SinSufijoProcesar_LanzaExcepcion()
    {
        var act = () => RutasLoteResolver.Resolver(
            @"\\192.168.0.69\ArchivosScaneados\alejandro.ortiz\2026-06-03");

        act.Should().Throw<InvalidOperationException>();
    }
}

public class LogDiarioServiceTests
{
    [Fact]
    public async Task IncrementarAsync_AcumulaContadoresEnArchivoDiario()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var ctx = new RutasLoteContext
        {
            Usuario = "test.user",
            Fecha = "2026-06-03",
            Procesar = Path.Combine(temp, "procesar"),
            Procesando = Path.Combine(temp, "procesando"),
            Error = Path.Combine(temp, "error"),
            Procesaria = Path.Combine(temp, "procesaria"),
            Noprocesados = Path.Combine(temp, "noprocesados"),
            Procesados = Path.Combine(temp, "procesados"),
            Log = Path.Combine(temp, "log")
        };

        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<LogDiarioService>.Instance;
        var service = new LogDiarioService(logger);

        Directory.CreateDirectory(ctx.Log); // simula carpeta creada por Worker 1

        await service.IncrementarAsync(ctx, 3, 1);
        await service.IncrementarAsync(ctx, 2, 4);

        var contenido = await File.ReadAllTextAsync(ctx.RutaLogDiario);
        contenido.Should().Contain("CantidadProcesados:5");
        contenido.Should().Contain("NoProcesados:5");

        Directory.Delete(temp, recursive: true);
    }
}

public class LoteProcesamientoServiceTxtTests
{
    [Fact]
    public async Task LeerRutaProcesarDesdeTxtAsync_LeeLinea1()
    {
        var temp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);

        var rutaProcesar = Path.Combine(temp, "procesar");
        Directory.CreateDirectory(rutaProcesar);

        var txt = Path.Combine(temp, "lote.txt");
        await File.WriteAllTextAsync(txt, $"{rutaProcesar}{Environment.NewLine}");

        var leida = await LoteProcesamientoService.LeerRutaProcesarDesdeTxtAsync(txt);

        leida.Should().Be(rutaProcesar);

        Directory.Delete(temp, recursive: true);
    }
}
