using Models.Dto;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using IWshRuntimeLibrary;
using File = System.IO.File;

namespace Infrastructure
{
    public class FileManagerInfraestructure
    {
        private readonly RutasSettings _rutas;
        private readonly ILogger<FileManagerInfraestructure> _logger;

        public FileManagerInfraestructure(
            IOptions<RutasSettings> rutasOptions,
            ILogger<FileManagerInfraestructure> logger)
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

        public void CrearAccesosDirectos()
        {
            string escritorio = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            CrearAccesoDirecto(escritorio, "Procesar", _rutas.Procesar);
            CrearAccesoDirecto(escritorio, "Errores", _rutas.Error);
        }

        private void CrearAccesoDirecto(string escritorio, string nombre, string rutaDestino)
        {
            string rutaAcceso = Path.Combine(escritorio, $"{nombre}.lnk");

            if (File.Exists(rutaAcceso))
            {
                _logger.LogInformation($"Acceso directo ya existe: {rutaAcceso}");
                return;
            }

            var shell = new WshShell();
            var acceso = (IWshShortcut)shell.CreateShortcut(rutaAcceso);

            acceso.TargetPath = rutaDestino;
            acceso.WorkingDirectory = rutaDestino;
            acceso.Save();

            _logger.LogInformation($"Acceso directo creado: {rutaAcceso}");
        }
    }
}
