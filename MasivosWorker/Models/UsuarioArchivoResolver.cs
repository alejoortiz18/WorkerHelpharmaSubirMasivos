namespace Models
{
    /// <summary>
    /// Identifica el usuario propietario de un archivo a partir de su nombre.
    /// El usuario corresponde al texto ubicado antes del primer guion (-).
    /// Ejemplos:
    ///   usuariopik-2026-06-09 09-27-15AM.txt      -> usuariopik
    ///   dgutierrez-2026-06-09 07-29-15AM.txt      -> dgutierrez
    ///   alejandro.ortiz-2026-06-09 09-29-15AM.txt -> alejandro.ortiz
    /// </summary>
    public static class UsuarioArchivoResolver
    {
        public static string Resolver(string rutaOArchivo)
        {
            if (string.IsNullOrWhiteSpace(rutaOArchivo))
                throw new ArgumentException(
                    "No se puede resolver el usuario de un nombre de archivo vacío.",
                    nameof(rutaOArchivo));

            var nombre = Path.GetFileName(rutaOArchivo);
            var guion = nombre.IndexOf('-');
            var usuario = guion > 0 ? nombre[..guion] : Path.GetFileNameWithoutExtension(nombre);

            return usuario.Trim().ToLowerInvariant();
        }
    }
}
