using FluentAssertions;
using GestionArchivosEscaneados.Constants;

namespace GestionArchivosEscaneados.Tests;

public class AdministradorPortalTests
{
    [Theory]
    [InlineData("alejandro.ortiz", true)]
    [InlineData("ALEJANDRO.ORTIZ", true)]
    [InlineData("otro.usuario", false)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void EsAdministrador_SoloAlejandroOrtiz(string? usuario, bool esperado)
    {
        AdministradorPortal.EsAdministrador(usuario).Should().Be(esperado);
    }
}
