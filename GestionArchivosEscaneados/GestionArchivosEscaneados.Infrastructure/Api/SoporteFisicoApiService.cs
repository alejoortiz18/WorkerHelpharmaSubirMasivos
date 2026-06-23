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
    private readonly string _idUsuario;

    public SoporteFisicoApiService(
        HttpClient httpClient,
        ILogger<SoporteFisicoApiService> logger,
        IOptions<ApiCredentialsSettings> credenciales)
    {
        _httpClient = httpClient;
        _logger = logger;
        _token = credenciales.Value.SoporteFisicoToken;
        _idUsuario = credenciales.Value.IdUsuario;
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
            var idUsuarioEnvio = string.IsNullOrWhiteSpace(_idUsuario) ? idUsuario : _idUsuario;
            form.Add(new StringContent(idUsuarioEnvio), "idUsuario");

            var medicamentosJson = JsonSerializer.Serialize(data.medicamentos ?? []);
            form.Add(new StringContent(medicamentosJson, Encoding.UTF8, "application/json"), "medicamentos");

            var fileContent = new ByteArrayContent(contenidoPdf);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            form.Add(fileContent, "anexo", nombreArchivo);

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
