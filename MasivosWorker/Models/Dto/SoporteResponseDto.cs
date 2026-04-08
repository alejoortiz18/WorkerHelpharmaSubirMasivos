using System;
using System.Collections.Generic;
using System.Text;

namespace Models.Dto
{
    public class SoporteResponseDto
    {
        public string idConvenio { get; set; }
        public string nombreConvenio { get; set; }
        public DateTime fecha { get; set; }
        public string idBodega { get; set; }
        public string nombreSede { get; set; }
        public string tipoEntrega { get; set; }
        public string tipoPlan { get; set; }
        public string idCartera { get; set; }
        public string nombrePaciente { get; set; }
        public string celular { get; set; }
        public string direccion { get; set; }

        public List<MedicamentoDto> medicamentos { get; set; }
    }

   
}
