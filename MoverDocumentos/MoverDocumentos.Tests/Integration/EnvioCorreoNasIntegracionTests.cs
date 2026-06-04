using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MoverDocumentos.Core.Configuration;
using MoverDocumentos.Core.Services;
using Xunit;

namespace MoverDocumentos.Tests.Integration;

/// <summary>
/// Envío real de correo NAS (Worker 1). Requiere Email:SmtpHost en user-secrets o variables de entorno.
///
///   dotnet test --filter "EnviarCorreoNasReal_MuestraFormatoAlDestinatario"
/// </summary>
public class EnvioCorreoNasIntegracionTests
{
    [Fact]
    public async Task EnviarCorreoNasReal_MuestraFormatoAlDestinatario()
    {
        var settings = CargarEmailSettings();
        if (!settings.Habilitado)
        {
            Assert.Fail(
                "SMTP no configurado. Configure Email:SmtpHost, Email:Usuario, Email:Clave " +
                "en user-secrets de MoverDocumentos o variables Email__*.");
        }

        var servicio = new EmailNotificationService(
            Options.Create(settings),
            NullLogger<EmailNotificationService>.Instance);

        var usuario = Environment.GetEnvironmentVariable("MOVERS_USUARIO_PRUEBA") ?? "alejandro.ortiz";
        var fecha = DateOnly.FromDateTime(DateTime.Now);
        var ruta = $@"\\192.168.0.69\ArchivosScaneados\{usuario}\{fecha:yyyy-MM-dd}\procesar";
        var error =
            "[PRUEBA MANUAL Worker 1] Simulación de fallo de conexión a la NAS al subir archivos a procesar.";

        await servicio.EnviarFalloNasAsync(
            usuario,
            fecha,
            cantidadArchivos: 2,
            rutaProcesar: ruta,
            errorMensaje: error);

        var cuerpoEsperado = EmailNotificationService.ConstruirCuerpoEsperado(
            usuario,
            fecha,
            2,
            ruta,
            error);

        cuerpoEsperado.Should().Contain(usuario);
        cuerpoEsperado.Should().Contain("NAS (procesar)");
    }

    private static EmailSettings CargarEmailSettings()
    {
        var settings = new EmailSettings
        {
            Remitente = Environment.GetEnvironmentVariable("Email__Remitente")
                ?? "sistemas.helpharma@zentria.com.co",
            SmtpHost = Environment.GetEnvironmentVariable("Email__SmtpHost") ?? string.Empty,
            SmtpPort = int.TryParse(Environment.GetEnvironmentVariable("Email__SmtpPort"), out var port)
                ? port
                : 587,
            Usuario = Environment.GetEnvironmentVariable("Email__Usuario") ?? string.Empty,
            Clave = Environment.GetEnvironmentVariable("Email__Clave") ?? string.Empty,
            Destinatarios =
            [
                Environment.GetEnvironmentVariable("Email__DestinatarioPrueba")
                    ?? "alejandro.ortiz@zentria.com.co"
            ]
        };

        return settings;
    }
}
