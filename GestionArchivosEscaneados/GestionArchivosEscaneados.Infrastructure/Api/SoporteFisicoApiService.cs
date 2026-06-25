using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GestionArchivosEscaneados.Infrastructure.Configuracion;
using GestionArchivosEscaneados.Models.Dto;
using Microsoft.Extensions.Logging;

namespace GestionArchivosEscaneados.Infrastructure.Api;

public class SoporteFisicoApiService
{
    private readonly HttpClient _httpClient;
    private readonly IIntegracionConfigProvider _config;
    private readonly ILogger<SoporteFisicoApiService> _logger;

    public SoporteFisicoApiService(
        HttpClient httpClient,
        IIntegracionConfigProvider config,
        ILogger<SoporteFisicoApiService> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
    }

    public async Task<bool> EnviarSoporteFisicoAsync(
        string soporte,
        byte[] contenidoPdf,
        string nombreArchivo,
        SoporteResponseDto data,
        string idUsuario)
    {
        try
        {
            var endpoint = await _config.ObtenerSoporteFisicoApiUrlAsync();
            var token = await _config.ObtenerSoporteFisicoTokenAsync();
            var idUsuarioConfig = await _config.ObtenerIdUsuarioSoporteFisicoAsync();
            var idUsuarioEnvio = string.IsNullOrWhiteSpace(idUsuarioConfig) ? idUsuario : idUsuarioConfig;

            var soporteNormalizado = NormalizarSoporte(soporte);
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(soporteNormalizado), "soporte");
            form.Add(new StringContent(NormalizarIdConvenio(data.IdConvenio)), "idConvenio");
            form.Add(new StringContent(data.NombreConvenio ?? ""), "nombreConvenio");
            form.Add(new StringContent(data.Fecha.ToString("yyyy-MM-dd HH:mm:ss")), "fecha");
            form.Add(new StringContent(data.IdBodega ?? ""), "idBodega");
            form.Add(new StringContent(data.NombreSede ?? ""), "nombreSede");
            form.Add(new StringContent(data.NombreActividad ?? ""), "nombreActividad");
            form.Add(new StringContent(data.TipoEntrega ?? ""), "tipoEntrega");
            form.Add(new StringContent(data.TipoPlan ?? ""), "tipoPlan");
            form.Add(new StringContent(data.IdCartera ?? ""), "idCartera");
            form.Add(new StringContent(data.NombrePaciente ?? ""), "nombrePaciente");
            form.Add(new StringContent(data.IdTipoId ?? ""), "idTipoId");
            form.Add(new StringContent(data.IdPaciente.ToString()), "idPaciente");
            form.Add(new StringContent(data.Celular ?? ""), "celular");
            form.Add(new StringContent(data.Telefono ?? ""), "telefono");
            form.Add(new StringContent(data.Direccion ?? ""), "direccion");
            form.Add(new StringContent(data.Complemento ?? ""), "complemento");
            form.Add(new StringContent(data.Observacion ?? ""), "observacion");
            form.Add(new StringContent(""), "reclamante");
            form.Add(new StringContent(""), "idReclamante");
            form.Add(new StringContent(data.ValorCM ?? "0"), "valorCM");
            form.Add(new StringContent(idUsuarioEnvio), "idUsuario");

            var medicamentosJson = JsonSerializer.Serialize(data.medicamentos ?? []);
            form.Add(new StringContent(medicamentosJson, Encoding.UTF8, "application/json"), "medicamentos");

            var fileContent = new ByteArrayContent(contenidoPdf);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(fileContent, "anexo", nombreArchivo);

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = form;

            using var response = await _httpClient.SendAsync(request);
            var contenido = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SoporteFisicoOK | Soporte={Soporte}", soporteNormalizado);
                return true;
            }

            _logger.LogError(
                "SoporteFisicoError | Soporte={Soporte} | Status={Status} | Respuesta={Respuesta}",
                soporteNormalizado,
                response.StatusCode,
                contenido);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SoporteFisicoException | Soporte={Soporte}", soporte);
            return false;
        }
    }

    private static string NormalizarSoporte(string soporte) =>
        soporte.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

    private static string NormalizarIdConvenio(string? idConvenio)
    {
        if (string.IsNullOrWhiteSpace(idConvenio))
            return string.Empty;

        var limpio = idConvenio.Trim();
        return int.TryParse(limpio, out var numero)
            ? numero.ToString()
            : limpio;
    }
}
