using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;

namespace Services;

public class EmailNotificationService : IEmailNotificationService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailNotificationService> _logger;

    public EmailNotificationService(
        IOptions<EmailSettings> settings,
        ILogger<EmailNotificationService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task EnviarFalloOpenAiLoteAsync(
        RutasLoteContext contexto,
        int cantidadArchivos,
        string errorMensaje,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Habilitado)
        {
            _logger.LogWarning(
                "CorreoNoEnviado | Motivo=SmtpNoConfigurado | Usuario={Usuario} | Fecha={Fecha}",
                contexto.Usuario,
                contexto.Fecha);
            return;
        }

        var cuerpo =
            $"El usuario {contexto.Usuario} ha escaneado {cantidadArchivos} archivos del día {contexto.Fecha} " +
            $"y al subirlos a OpenAI se presentó el siguiente error:{Environment.NewLine}{Environment.NewLine}" +
            $"Error:{Environment.NewLine}{errorMensaje}{Environment.NewLine}{Environment.NewLine}" +
            $"Ruta:{Environment.NewLine}{contexto.Noprocesados}";

        using var mensaje = new MailMessage
        {
            From = new MailAddress(_settings.Remitente),
            Subject = $"Fallo OpenAI — lote {contexto.Usuario} {contexto.Fecha}",
            Body = cuerpo,
            IsBodyHtml = false
        };

        foreach (var destinatario in _settings.Destinatarios.Where(d => !string.IsNullOrWhiteSpace(d)))
            mensaje.To.Add(destinatario);

        using var cliente = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = true,
            Credentials = string.IsNullOrWhiteSpace(_settings.Usuario)
                ? CredentialCache.DefaultNetworkCredentials
                : new NetworkCredential(_settings.Usuario, _settings.Clave)
        };

        cancellationToken.ThrowIfCancellationRequested();

        await cliente.SendMailAsync(mensaje, cancellationToken);

        _logger.LogInformation(
            "CorreoOpenAiEnviado | Usuario={Usuario} | Fecha={Fecha} | Destinatarios={Cantidad}",
            contexto.Usuario,
            contexto.Fecha,
            mensaje.To.Count);
    }
}
