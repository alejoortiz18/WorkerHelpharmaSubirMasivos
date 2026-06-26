namespace GestionArchivosEscaneados.Constants;

public static class MensajesUsuario
{
    public const string UsuarioNoRegistrado =
        "No ha subido archivos al sistema.\nEn caso de dudas contacte al administrador.";

    public const string BaseDatosNoAccesible =
        "No se puede acceder a la base de datos de trazabilidad.\nVerifique la conexion o contacte al administrador.";

    public const string DocumentoNoEncontrado =
        "No se encontro informacion del documento.\nContacta con el administrador.";

    public const string ErrorConsultaDatosSoportes =
        "No se encontro informacion del soporte en DatosSoportes.\nVerifique el codigo de barras o contacte al administrador.";

    public const string ErrorEnvioSoporteFisico =
        "Se encontro informacion del documento, pero fallo el envio al sistema de soporte fisico.\nContacta con el administrador.";

    public const string ErrorLecturaBarcode =
        "No se pudo identificar un codigo de barras valido para el documento.\nVerifica el codigo o contacta con el administrador.";

    public const string ErrorOpenAi =
        "No se pudo completar la lectura automatica del documento.\nIntenta nuevamente o contacta con el administrador.";

    public const string FechaNoDisponible =
        "La fecha seleccionada no existe en el sistema.";

    public const string ReprocesoExitoso =
        "Documento procesado correctamente.";

    public const string ReprocesoLoteExitoso =
        "{0} documento(s) procesado(s) correctamente.";

    public const string ReprocesoLoteParcial =
        "{0} documento(s) procesado(s). {1} documento(s) con error.";

    public const string ReprocesoLoteSinCodigos =
        "No hay documentos disponibles para reprocesar.";

    public const string ConfirmarEliminacionDocumento =
        "Esta seguro de eliminar este documento?\nEsta accion no se puede deshacer.";

    public const string EliminarExito =
        "Documento eliminado correctamente.";

    public const string EliminarError =
        "No se pudo eliminar el documento.";

    public const string UsuarioRequerido =
        "Debe iniciar sesion para continuar.";
}
