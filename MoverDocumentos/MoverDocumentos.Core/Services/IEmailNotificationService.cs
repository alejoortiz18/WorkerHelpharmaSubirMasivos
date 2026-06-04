namespace MoverDocumentos.Core.Services;

public interface IEmailNotificationService
{
    Task EnviarFalloNasAsync(
        string usuario,
        DateOnly fecha,
        int cantidadArchivos,
        string rutaProcesar,
        string errorMensaje,
        CancellationToken cancellationToken = default);
}
