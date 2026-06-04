using GestionArchivosEscaneados.Models.Enums;

namespace GestionArchivosEscaneados.Models.Dto;

public class SoporteProcesamientoResult
{
    public SoporteProcesamientoEstado Estado { get; init; }

    public string Soporte { get; init; } = string.Empty;

    public SoporteResponseDto? Datos { get; init; }

    public bool EsExitoso => Estado == SoporteProcesamientoEstado.Exito;
}
