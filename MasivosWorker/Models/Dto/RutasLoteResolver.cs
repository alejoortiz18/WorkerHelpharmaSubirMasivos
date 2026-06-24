namespace Models.Dto;

public static class RutasLoteResolver
{
    public static RutasLoteContext Resolver(string rutaProcesar)
    {
        if (string.IsNullOrWhiteSpace(rutaProcesar))
            throw new ArgumentException("La ruta procesar no puede estar vacía.", nameof(rutaProcesar));

        var procesar = Path.GetFullPath(rutaProcesar.Trim())
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var nombreCarpeta = Path.GetFileName(procesar);
        if (!string.Equals(nombreCarpeta, "procesar", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"La ruta del lote debe terminar en \\procesar. Recibido: {procesar}");
        }

        var carpetaDia = Path.GetDirectoryName(procesar)
            ?? throw new InvalidOperationException($"No se pudo resolver la carpeta del día: {procesar}");

        var carpetaUsuario = Path.GetDirectoryName(carpetaDia)
            ?? throw new InvalidOperationException($"No se pudo resolver la carpeta del usuario: {procesar}");

        var usuario = Path.GetFileName(carpetaUsuario);
        var fecha = Path.GetFileName(carpetaDia);

        if (string.IsNullOrWhiteSpace(usuario) || string.IsNullOrWhiteSpace(fecha))
        {
            throw new InvalidOperationException(
                $"No se pudo extraer usuario/fecha desde: {procesar}");
        }

        return new RutasLoteContext
        {
            Usuario = usuario,
            Fecha = fecha,
            Procesar = procesar,
            Procesando = Path.Combine(carpetaDia, "procesando"),
            Error = Path.Combine(carpetaDia, "error"),
            Procesaria = Path.Combine(carpetaDia, "procesaria"),
            Noprocesados = Path.Combine(carpetaDia, "noprocesados"),
            Procesados = Path.Combine(carpetaDia, "procesados")
        };
    }
}
