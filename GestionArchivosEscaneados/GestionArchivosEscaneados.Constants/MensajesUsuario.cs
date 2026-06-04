namespace GestionArchivosEscaneados.Constants;

public static class MensajesUsuario
{
    public const string UsuarioNoRegistrado =
        "No ha subido archivos al sistema.\nEn caso de dudas contacte al administrador.";

    public const string DocumentoNoEncontrado =
        "No se encontró información del documento.\nContacta con el administrador.";

    public const string FechaNoDisponible =
        "La fecha seleccionada no existe en el sistema.";

    public const string ReprocesoExitoso =
        "Documento procesado correctamente.";

    public const string ReprocesoLoteExitoso =
        "{0} documento(s) procesado(s) correctamente.";

    public const string ReprocesoLoteParcial =
        "{0} documento(s) procesado(s). {1} documento(s) con error.";

    public const string ReprocesoLoteSinCodigos =
        "Ingrese al menos un código de barras para procesar.";

    public const string ConfirmarEliminacionDocumento =
        "¿Está seguro de eliminar este documento?\nEsta acción no se puede deshacer.";

    public const string EliminarExito =
        "Documento eliminado correctamente.";

    public const string EliminarError =
        "No se pudo eliminar el documento.";

    public const string UsuarioRequerido =
        "Debe iniciar sesión para continuar.";
}
