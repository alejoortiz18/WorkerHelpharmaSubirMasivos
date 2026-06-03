using System;
using System.Collections.Generic;
using System.Text;

namespace Models
{
    public class FileSettings
    {
        public string KeyName { get; set; } = string.Empty;
        public int MaxArchivosConcurrentes { get; set; } = 2;
        public int BarcodeMaxReintentos { get; set; } = 3;
        public int BarcodeEsperaMs { get; set; } = 500;
        /// <summary>Reintentos al esperar que el PDF termine de copiarse o se libere (antivirus, red).</summary>
        public int ArchivoEsperaIntentos { get; set; } = 120;
        public int ArchivoEsperaMs { get; set; } = 500;
        /// <summary>Lecturas consecutivas con el mismo tamaño para considerar el archivo estable.</summary>
        public int ArchivoLecturasEstables { get; set; } = 2;
    }
}
