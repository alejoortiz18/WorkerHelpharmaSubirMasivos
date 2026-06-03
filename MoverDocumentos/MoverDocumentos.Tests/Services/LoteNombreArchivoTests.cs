using System.Globalization;
using FluentAssertions;
using Xunit;

namespace MoverDocumentos.Tests.Services;

public class LoteNombreArchivoTests
{
    [Fact]
    public void FormatoNombreTxt_IncluyeHoraMinutoSegundoSeparadosPorGuion()
    {
        var fecha = new DateOnly(2026, 6, 3);
        var instante = new DateTime(2026, 6, 3, 8, 10, 20);
        var formatoHora = "hh-mm-sstt";

        var hora = instante.ToString(formatoHora, CultureInfo.InvariantCulture);
        var nombre = $"alejandro.ortiz-{fecha:yyyy-MM-dd} {hora}.txt";

        nombre.Should().Be("alejandro.ortiz-2026-06-03 08-10-20AM.txt");
    }
}
