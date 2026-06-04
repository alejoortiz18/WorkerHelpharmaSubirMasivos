using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models;
using Models.Dto;
using File = System.IO.File;

namespace Infrastructure
{
    public class FileManagerInfraestructure
    {
        private readonly ILogger<FileManagerInfraestructure> _logger;
        private readonly string _fileName;
        private readonly bool _aplicarPrefijoKeyName;

        public FileManagerInfraestructure(
            IOptions<FileSettings> fileOptions,
            ILogger<FileManagerInfraestructure> logger)
        {
            _logger = logger;
            _fileName = fileOptions.Value.KeyName;
            _aplicarPrefijoKeyName = fileOptions.Value.AplicarPrefijoKeyName;
        }

        /// <summary>
        /// Las carpetas del lote las crea Worker 1; Worker 2 solo valida que existan.
        /// </summary>
        public void ValidarCarpetasLoteExisten(RutasLoteContext contexto)
        {
            foreach (var carpeta in contexto.CarpetasOperativas)
            {
                if (!Directory.Exists(carpeta))
                {
                    throw new InvalidOperationException(
                        $"Carpeta requerida no existe (debe ser creada previamente): {carpeta}");
                }
            }
        }

        public string MoverAProcesando(string rutaOrigen, RutasLoteContext contexto) =>
            MoverArchivo(rutaOrigen, contexto.Procesando, "PROCESANDO");

        public void MoverAProcesados(string rutaOrigen, string nuevoNombre, RutasLoteContext contexto) =>
            MoverArchivoConNombre(rutaOrigen, contexto.Procesados, nuevoNombre, "PROCESADOS");

        public void MoverAError(string rutaOrigen, RutasLoteContext contexto) =>
            MoverArchivo(rutaOrigen, contexto.Error, "ERROR");

        public void MoverAProcesaria(string rutaOrigen, RutasLoteContext contexto) =>
            MoverArchivo(rutaOrigen, contexto.Procesaria, "PROCESARIA");

        public void MoverANoprocesados(string rutaOrigen, RutasLoteContext contexto) =>
            MoverArchivo(rutaOrigen, contexto.Noprocesados, "NOPROCESADOS");

        public void LimpiarArchivosTemporales(RutasLoteContext contexto)
        {
            foreach (var carpeta in contexto.CarpetasLimpieza)
                EliminarArchivosEnCarpeta(carpeta);
        }

        public void EliminarArchivosEnCarpeta(string carpeta)
        {
            if (!Directory.Exists(carpeta))
                return;

            foreach (var archivo in Directory.GetFiles(carpeta))
            {
                try
                {
                    File.Delete(archivo);
                    _logger.LogDebug("ArchivoEliminado | Ruta={Ruta}", archivo);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "ErrorEliminandoArchivo | Ruta={Ruta}", archivo);
                }
            }
        }

        private string MoverArchivo(
            string rutaOrigen,
            string carpetaDestino,
            string etiqueta)
        {
            var nombre = Path.GetFileName(rutaOrigen);
            var nombreFinal = NormalizarNombre(nombre);
            var destino = Path.Combine(carpetaDestino, nombreFinal);

            File.Move(rutaOrigen, destino, true);

            _logger.LogInformation(
                "ArchivoMovido | Destino={Destino} | Archivo={Archivo}",
                etiqueta,
                nombreFinal);

            return destino;
        }

        private void MoverArchivoConNombre(
            string rutaOrigen,
            string carpetaDestino,
            string nuevoNombre,
            string etiqueta)
        {
            var destino = Path.Combine(carpetaDestino, NormalizarNombre(nuevoNombre));

            File.Move(rutaOrigen, destino, true);

            _logger.LogInformation(
                "ArchivoMovido | Destino={Destino} | Archivo={Archivo}",
                etiqueta,
                Path.GetFileName(destino));
        }

        private string NormalizarNombre(string nombreArchivo)
        {
            if (!_aplicarPrefijoKeyName)
                return nombreArchivo;

            return NormalizarNombreConPrefijo(nombreArchivo, _fileName);
        }

        /// <summary>
        /// Quita prefijos repetidos al inicio y deja exactamente uno.
        /// </summary>
        public static string NormalizarNombreConPrefijo(string nombreArchivo, string prefijo)
        {
            if (string.IsNullOrEmpty(prefijo))
                return nombreArchivo;

            var nombre = nombreArchivo;
            while (nombre.StartsWith(prefijo, StringComparison.OrdinalIgnoreCase))
                nombre = nombre[prefijo.Length..];

            return $"{prefijo}{nombre}";
        }

        public static string AplicarPrefijoSiFalta(string nombreArchivo, string prefijo) =>
            NormalizarNombreConPrefijo(nombreArchivo, prefijo);
    }
}
