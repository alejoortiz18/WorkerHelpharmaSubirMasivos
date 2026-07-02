using System.Text.Json;
using FluentAssertions;
using GestionArchivosEscaneados.Models.Dto;

namespace GestionArchivosEscaneados.Tests.Infrastructure;

public class SoporteResponseDeserializationTests
{
    [Fact]
    public void Deserialize_IdPacienteComoCadena_ProduceEntero()
    {
        const string json = """
            {
              "idConvenio": "01",
              "nombrePaciente": "HIJO DE  MEJIA RAMIREZ",
              "idPaciente": "30233836",
              "fecha": "2026-06-11T00:00:00"
            }
            """;

        var dto = JsonSerializer.Deserialize<SoporteResponseDto>(json, SoporteApiServiceJson.Options);

        dto.Should().NotBeNull();
        dto!.IdPaciente.Should().Be("30233836");
        dto.NombrePaciente.Should().Be("HIJO DE  MEJIA RAMIREZ");
    }

    [Fact]
    public void Deserialize_IdPacienteComoNumero_ProduceEntero()
    {
        const string json = """
            {
              "idPaciente": 30233836,
              "nombrePaciente": "Paciente"
            }
            """;

        var dto = JsonSerializer.Deserialize<SoporteResponseDto>(json, SoporteApiServiceJson.Options);

        dto.Should().NotBeNull();
        dto!.IdPaciente.Should().Be("30233836");
    }
}

internal static class SoporteApiServiceJson
{
    internal static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };
}
