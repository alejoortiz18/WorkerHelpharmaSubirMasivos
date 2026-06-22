using Models.Dto;
using Services;

namespace Tests.Infrastructure;

/// <summary>
/// Captura los argumentos del correo y opcionalmente delega al SMTP real.
/// Permite validar que la ruta del correo proviene del lote procesado, no está quemada.
/// </summary>
public sealed class EmailNotificacionCaptura : IEmailNotificationService
{
    private readonly IEmailNotificationService? _delegado;

    public RutasLoteContext? UltimoContexto { get; private set; }

    public int UltimaCantidadArchivos { get; private set; }

    public string? UltimoErrorMensaje { get; private set; }

    public int Invocaciones { get; private set; }

    public EmailNotificacionCaptura(IEmailNotificationService? delegado = null)
    {
        _delegado = delegado;
    }

    public async Task EnviarFalloOpenAiLoteAsync(
        RutasLoteContext contexto,
        int cantidadArchivos,
        string errorMensaje,
        CancellationToken cancellationToken = default)
    {
        Invocaciones++;
        UltimoContexto = contexto;
        UltimaCantidadArchivos = cantidadArchivos;
        UltimoErrorMensaje = errorMensaje;

        if (_delegado != null)
            await _delegado.EnviarFalloOpenAiLoteAsync(
                contexto,
                cantidadArchivos,
                errorMensaje,
                cancellationToken);
    }

    public static string ConstruirCuerpoEsperado(
        RutasLoteContext contexto,
        int cantidadArchivos,
        string errorMensaje) =>
        $"El usuario {contexto.Usuario} ha escaneado {cantidadArchivos} archivos del día {contexto.Fecha} " +
        $"y al subirlos a OpenAI se presentó el siguiente error:{Environment.NewLine}{Environment.NewLine}" +
        $"Error:{Environment.NewLine}{errorMensaje}{Environment.NewLine}{Environment.NewLine}" +
        $"Ruta:{Environment.NewLine}{contexto.Noprocesados}";
}
