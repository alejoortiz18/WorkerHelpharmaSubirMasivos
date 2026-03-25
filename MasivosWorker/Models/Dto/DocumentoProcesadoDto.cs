using System;
using System.Collections.Generic;
using System.Text;

namespace Models.Dto
{
    public class DocumentoProcesadoDto
    {
        public string Prefijo { get; set; }

        public string Numero { get; set; }

        public string NombreArchivo { get; set; }

        public byte[] Archivo { get; set; }
    }
}
