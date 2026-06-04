using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MoverDocumentos.Core.Configuration;

namespace MoverDocumentos.Core.Services;

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

    public async Task EnviarFalloNasAsync(
        string usuario,
        DateOnly fecha,
        int cantidadArchivos,
        string rutaProcesar,
        string errorMensaje,
        CancellationToken cancellationToken = default)
    {
        if (!_settings.Habilitado)
        {
            _logger.LogWarning(
                "CorreoNoEnviado | Motivo=SmtpNoConfigurado | Usuario={Usuario} | Fecha={Fecha}",
                usuario,
                fecha);
            return;
        }

        var cuerpo =
            $"El usuario {usuario} ha escaneado {cantidadArchivos} archivos del día {fecha:yyyy-MM-dd} " +
            $"y al subirlos a la NAS (procesar) se presentó el siguiente error:{Environment.NewLine}{Environment.NewLine}" +
            $"Error:{Environment.NewLine}{errorMensaje}{Environment.NewLine}{Environment.NewLine}" +
            $"Ruta:{Environment.NewLine}{rutaProcesar}";

        using var mensaje = new MailMessage
        {
            From = new MailAddress(_settings.Remitente),
            Subject = $"Fallo NAS — {usuario} {fecha:yyyy-MM-dd}",
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
            "CorreoNasEnviado | Usuario={Usuario} | Fecha={Fecha} | Destinatarios={Cantidad}",
            usuario,
            fecha,
            mensaje.To.Count);
    }

    public static string ConstruirCuerpoEsperado(
        string usuario,
        DateOnly fecha,
        int cantidadArchivos,
        string rutaProcesar,
        string errorMensaje) =>
        $"El usuario {usuario} ha escaneado {cantidadArchivos} archivos del día {fecha:yyyy-MM-dd} " +
        $"y al subirlos a la NAS (procesar) se presentó el siguiente error:{Environment.NewLine}{Environment.NewLine}" +
        $"Error:{Environment.NewLine}{errorMensaje}{Environment.NewLine}{Environment.NewLine}" +
        $"Ruta:{Environment.NewLine}{rutaProcesar}";
}
