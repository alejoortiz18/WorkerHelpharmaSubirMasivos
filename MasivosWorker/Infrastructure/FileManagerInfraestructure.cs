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
            CrearCarpeta(_rutas.Procesando); // 🔥 NUEVO
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
        }

        public void CrearAccesosDirectos()
        {
            string escritorio = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

            CrearAccesoDirecto(escritorio, "Procesar", _rutas.Procesar);
            CrearAccesoDirecto(escritorio, "Procesando", _rutas.Procesando); // 🔥 NUEVO
            CrearAccesoDirecto(escritorio, "Errores", _rutas.Error);
        }

        private void CrearAccesoDirecto(string escritorio, string nombre, string rutaDestino)
        {
            string rutaAcceso = Path.Combine(escritorio, $"{nombre}.lnk");

            if (File.Exists(rutaAcceso))
                return;

            var shell = new WshShell();
            var acceso = (IWshShortcut)shell.CreateShortcut(rutaAcceso);

            acceso.TargetPath = rutaDestino;
            acceso.WorkingDirectory = rutaDestino;
            acceso.Save();

            _logger.LogInformation($"Acceso directo creado: {rutaAcceso}");
        }

        // 🔥 NUEVO MÉTODO CLAVE
        public string MoverAProcesando(string rutaOrigen)
        {
            var nombre = Path.GetFileName(rutaOrigen);
            var destino = Path.Combine(_rutas.Procesando, nombre);

            File.Move(rutaOrigen, destino, true);

            _logger.LogInformation($"Archivo movido a PROCESANDO: {nombre}");

            return destino;
        }

        public void MoverAProcesados(string rutaOrigen, string nuevoNombre)
        {
            var destino = Path.Combine(_rutas.Procesados, nuevoNombre);

            File.Move(rutaOrigen, destino, true);

            _logger.LogInformation($"Archivo movido a PROCESADOS: {nuevoNombre}");
        }

        public void MoverAError(string rutaOrigen)
        {
            var nombre = Path.GetFileName(rutaOrigen);
            var destino = Path.Combine(_rutas.Error, nombre);

            File.Move(rutaOrigen, destino, true);

            _logger.LogWarning($"Archivo movido a ERROR: {nombre}");
        }
    }
}