using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionArchivosEscaneados.Infrastructure.Api;

public class SoporteFisicoApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SoporteFisicoApiService> _logger;
    private readonly string _token;

    public SoporteFisicoApiService(
        HttpClient httpClient,
        ILogger<SoporteFisicoApiService> logger,
        IOptions<ApiCredentialsSettings> credenciales)
    {
        _httpClient = httpClient;
        _logger = logger;
        _token = credenciales.Value.SoporteFisicoToken;
    }

    public async Task<bool> EnviarSoporteFisicoAsync(
        string soporte,
        string rutaArchivo,
        SoporteResponseDto data,
        string idUsuario)
    {
        try
        {
            var idConvenio = int.TryParse(data.IdConvenio, out var parsed) ? parsed : 0;

            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(soporte), "soporte");
            form.Add(new StringContent(idConvenio.ToString()), "idConvenio");
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
            form.Add(new StringContent(data.ValorCM ?? "0"), "valorCM");
            form.Add(new StringContent(idUsuario), "idUsuario");

            var medicamentosJson = JsonSerializer.Serialize(data.medicamentos ?? []);
            form.Add(new StringContent(medicamentosJson, Encoding.UTF8, "application/json"), "medicamentos");

            var fileBytes = await File.ReadAllBytesAsync(rutaArchivo);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(fileContent, "anexo", Path.GetFileName(rutaArchivo));

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://intranet.helpharma.com/api/v1/soporte/fisico");

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = form;

            using var response = await _httpClient.SendAsync(request);
            var contenido = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("SoporteFisicoOK | Soporte={Soporte}", soporte);
                return true;
            }

            _logger.LogError(
                "SoporteFisicoError | Soporte={Soporte} | Status={Status} | Respuesta={Respuesta}",
                soporte,
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
}
