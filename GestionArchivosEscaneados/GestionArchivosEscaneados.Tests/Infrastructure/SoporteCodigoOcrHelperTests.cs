using FluentAssertions;
using GestionArchivosEscaneados.Infrastructure.Api;

namespace GestionArchivosEscaneados.Tests.Infrastructure;

public class SoporteCodigoOcrHelperTests
{
    [Fact]
    public void VariantesConsultaDatosSoportes_FEMI_IncluyeFMI()
    {
        var variantes = SoporteCodigoOcrHelper.VariantesConsultaDatosSoportes("FEMI-401565").ToList();

        variantes.Should().Contain("FEMI-401565");
        variantes.Should().Contain("FMI-401565");
        variantes.Should().Contain("FMI401565");
        variantes.Should().Contain("IM401565");
        variantes.IndexOf("FEMI-401565").Should().BeLessThan(variantes.IndexOf("IM401565"));
    }

    [Fact]
    public void VariantesConfusionI1_FM161068_IncluyeFMI61068()
    {
        var variantes = SoporteCodigoOcrHelper.VariantesConfusionI1("FM161068").ToList();

        variantes.Should().Contain("FM161068");
        variantes.Should().Contain("FMI61068");
    }
}
