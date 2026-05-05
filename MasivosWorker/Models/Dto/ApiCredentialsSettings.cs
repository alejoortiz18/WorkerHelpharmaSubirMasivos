using System;
using System.Collections.Generic;
using System.Text;

namespace Models.Dto
{
    public class ApiCredentialsSettings
    {
        public string SoporteApiKey { get; set; } = string.Empty;
        public string SoporteFisicoToken { get; set; } = string.Empty;
        public string IdUsuario { get; set; } = "system";
    }
}
