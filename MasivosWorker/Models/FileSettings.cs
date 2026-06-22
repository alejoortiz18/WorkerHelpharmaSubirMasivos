namespace Models
{
    public class FileSettings
    {
        public string KeyName { get; set; } = string.Empty;

        /// <summary>Cantidad de PDF por tanda (RF-04).</summary>
        public int TamanoLote { get; set; } = 4;

        /// <summary>Aplicar prefijo KeyName al renombrar archivos en el lote.</summary>
        public bool AplicarPrefijoKeyName { get; set; }

        public int MaxArchivosConcurrentes { get; set; } = 4;

        /// <summary>
        /// Cantidad máxima de lotes (TXT) que se procesan en paralelo en hilos
        /// independientes. Parametrizable sin recompilar (RF-03, RF-04).
        /// Nunca se procesan dos archivos del mismo usuario al mismo tiempo.
        /// </summary>
        public int MaxProcesosSimultaneos { get; set; } = 2;
        public int BarcodeMaxReintentos { get; set; } = 3;
        public int BarcodeEsperaMs { get; set; } = 500;
        public int ArchivoEsperaIntentos { get; set; } = 120;
        public int ArchivoEsperaMs { get; set; } = 500;
        public int ArchivoLecturasEstables { get; set; } = 2;

        /// <summary>Intervalo de sondeo cuando ArchivosNuevos está vacío (segundos).</summary>
        public int ArchivosNuevosEscaneoSegundos { get; set; } = 5;
    }
}
