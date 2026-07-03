using FluentAssertions;
using Infrastructure;
using Models.Dto;
using Xunit;

namespace Tests.Infrastructure;

public class RadicaWebCombinacionAcumuladorTests
{
    [Fact]
    public void AgregarSiExitoso_DeduplicaMismaFechaYBodega()
    {
        var acumulador = new RadicaWebCombinacionAcumulador();

        acumulador.AgregarSiExitoso(CrearExitoso("2026-07-02", "FARMACIAMEDELLIN"));
        acumulador.AgregarSiExitoso(CrearExitoso("2026-07-02", "farmaciamedellin"));
        acumulador.AgregarSiExitoso(CrearExitoso("2026-07-02", "FARMACIAMEDELLIN"));

        acumulador.Cantidad.Should().Be(1);
        acumulador.ObtenerCombinaciones().Should().ContainSingle()
            .Which.Should().Be((new DateOnly(2026, 7, 2), "FARMACIAMEDELLIN"));
    }

    [Fact]
    public void AgregarSiExitoso_IgnoraNoExitosos()
    {
        var acumulador = new RadicaWebCombinacionAcumulador();

        acumulador.AgregarSiExitoso(new DocumentoProcesamientoResult
        {
            Estado = DocumentoProcesamientoEstado.FalloApiDatos,
            IdBodega = "FARMACIAMEDELLIN",
            FechaFactura = new DateTime(2026, 7, 2)
        });

        acumulador.Cantidad.Should().Be(0);
    }

    [Fact]
    public void AgregarSiExitoso_MantieneCombinacionesDistintas()
    {
        var acumulador = new RadicaWebCombinacionAcumulador();

        acumulador.AgregarSiExitoso(CrearExitoso("2026-07-02", "FARMACIAMEDELLIN"));
        acumulador.AgregarSiExitoso(CrearExitoso("2026-07-03", "FARMACIAMEDELLIN"));
        acumulador.AgregarSiExitoso(CrearExitoso("2026-07-02", "FARMACIABOGOTA"));

        acumulador.Cantidad.Should().Be(3);
    }

    private static DocumentoProcesamientoResult CrearExitoso(string fecha, string bodega) =>
        new()
        {
            Estado = DocumentoProcesamientoEstado.Exito,
            FechaFactura = DateTime.ParseExact(fecha, "yyyy-MM-dd", null),
            IdBodega = bodega
        };
}
