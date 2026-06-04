namespace Models.Dto
{
    public class RutasSettings
    {
        /// <summary>Raíz UNC compartida.</summary>
        public string RaizUnc { get; set; } = string.Empty;

        /// <summary>Subcarpeta relativa a RaizUnc donde Worker 1 deja los TXT de lote.</summary>
        public string ArchivosNuevos { get; set; } = "ArchivosNuevos";

        public string RutaArchivosNuevos =>
            string.IsNullOrWhiteSpace(RaizUnc)
                ? string.Empty
                : Path.Combine(RaizUnc.TrimEnd('\\'), ArchivosNuevos);
    }
}
