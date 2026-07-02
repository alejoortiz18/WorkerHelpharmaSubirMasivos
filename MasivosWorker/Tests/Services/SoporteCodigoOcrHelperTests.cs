using FluentAssertions;
using Services;
using Xunit;

namespace Tests.Services;

public class SoporteCodigoOcrHelperTests
{
    [Fact]
    public void VariantesConsultaDatosSoportes_FEMI_IncluyeIM()
    {
        var variantes = SoporteCodigoOcrHelper.VariantesConsultaDatosSoportes("FEMI-401565").ToList();

        variantes.Should().Contain("FEMI-401565");
        variantes.Should().Contain("IM-401565");
        variantes.Should().Contain("IM401565");
        variantes.Should().Contain("FMI-401565");
    }

    [Fact]
    public void VariantesConfusionI1_IntercambiaI1()
    {
        var variantes = SoporteCodigoOcrHelper.VariantesConfusionI1("FM161068").ToList();

        variantes.Should().Contain("FM161068");
        variantes.Should().Contain("FMI61068");
    }
}
