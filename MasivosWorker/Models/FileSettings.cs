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
    }
}
