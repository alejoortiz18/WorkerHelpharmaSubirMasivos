namespace Models.Dto;

/// <summary>
/// Resultado unificado del flujo de integración Helpharma (consulta por código + carga física).
/// Usado por MasivosWorker y por SitioVisualArchivosNoProcesados.
/// </summary>
public class SoporteProcesamientoResult
{
    public SoporteProcesamientoEstado Estado { get; init; }

    public string Soporte { get; init; } = string.Empty;

    public SoporteResponseDto? Datos { get; init; }

    public bool EsExitoso => Estado == SoporteProcesamientoEstado.Exito;
}

public enum SoporteProcesamientoEstado
{
    Exito,
    FalloApiDatos,
    FalloApiFisico
}
