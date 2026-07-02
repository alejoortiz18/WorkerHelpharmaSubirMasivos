namespace GestionArchivosEscaneados.Models.Dto;

public static class SoporteIdPacienteParser
{
    public static int? ParaSql(string? idPaciente)
    {
        if (string.IsNullOrWhiteSpace(idPaciente))
            return null;

        return int.TryParse(idPaciente.Trim(), out var id) ? id : null;
    }
}
