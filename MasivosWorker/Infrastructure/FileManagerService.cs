using Models.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure
{
    public class FileManagerService
    {
        private readonly RutasSettings _rutas;
        private readonly ILogger<FileManagerService> _logger;

        public FileManagerService(
            IOptions<RutasSettings> rutasOptions,
            ILogger<FileManagerService> logger)
        {
            _rutas = rutasOptions.Value;
            _logger = logger;
        }

        public void CrearCarpetasSiNoExisten()
        {
            CrearCarpeta(_rutas.Procesar);
            CrearCarpeta(_rutas.Error);
            CrearCarpeta(_rutas.Procesados);
        }

        private void CrearCarpeta(string ruta)
        {
            if (!Directory.Exists(ruta))
            {
                Directory.CreateDirectory(ruta);
                _logger.LogInformation($"Carpeta creada: {ruta}");
            }
            else
            {
                _logger.LogInformation($"Carpeta ya existe: {ruta}");
            }
        }
    }
}
