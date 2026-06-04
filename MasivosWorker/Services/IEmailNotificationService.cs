using Models.Dto;

namespace Services;

public interface IEmailNotificationService
{
    Task EnviarFalloOpenAiLoteAsync(
        RutasLoteContext contexto,
        int cantidadArchivos,
        string errorMensaje,
        CancellationToken cancellationToken = default);
}
