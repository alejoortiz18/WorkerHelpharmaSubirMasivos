namespace Infrastructure;

public enum LoteProcesamientoEstado
{
    Completado,
    PendienteRevision,
    PendienteReintento
}

public sealed class LoteProcesamientoOutcome
{
    public required LoteProcesamientoEstado Estado { get; init; }

    public int Procesados { get; init; }

    public int NoProcesados { get; init; }

    public bool PermiteContinuarInmediato => Estado == LoteProcesamientoEstado.Completado;

    public static LoteProcesamientoOutcome Completado(int procesados, int noProcesados) =>
        new()
        {
            Estado = LoteProcesamientoEstado.Completado,
            Procesados = procesados,
            NoProcesados = noProcesados
        };

    public static LoteProcesamientoOutcome PendienteRevision() =>
        new()
        {
            Estado = LoteProcesamientoEstado.PendienteRevision
        };

    public static LoteProcesamientoOutcome PendienteReintento(int procesados, int noProcesados) =>
        new()
        {
            Estado = LoteProcesamientoEstado.PendienteReintento,
            Procesados = procesados,
            NoProcesados = noProcesados
        };
}
