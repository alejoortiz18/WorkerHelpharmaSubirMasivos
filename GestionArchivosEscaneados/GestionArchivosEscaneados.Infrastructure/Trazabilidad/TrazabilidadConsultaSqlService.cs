using System.Globalization;
using GestionArchivosEscaneados.Constants;
using GestionArchivosEscaneados.Models.Dto;
using GestionArchivosEscaneados.Models.Entities;
using GestionArchivosEscaneados.Models.Settings;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GestionArchivosEscaneados.Infrastructure.Trazabilidad;

public interface ITrazabilidadConsultaSqlService
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task<bool> UsuarioExisteAsync(string nombreUsuario, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListarFechasDisponiblesAsync(string nombreUsuario, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CalendarioDiaResumen>> ListarResumenCalendarioPorMesAsync(
        string nombreUsuario,
        int anio,
        int mes,
        CancellationToken cancellationToken = default);

    Task<bool> FechaExisteAsync(string nombreUsuario, string fecha, CancellationToken cancellationToken = default);

    Task<ResumenLogDiario?> ObtenerResumenAsync(string nombreUsuario, string fecha, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentoPendiente>> ListarDocumentosPendientesAsync(
        string nombreUsuario,
        string fecha,
        CancellationToken cancellationToken = default);

    Task<bool> DocumentoPendienteExisteAsync(
        string nombreUsuario,
        string fecha,
        string nombreArchivo,
        CancellationToken cancellationToken = default);

    Task<bool> MarcarDocumentoProcesadoAsync(
        string nombreUsuario,
        string fecha,
        string nombreArchivo,
        string? soporte,
        int? idPaciente,
        string? idBodega,
        string? idCartera,
        DateTime? fechaFactura,
        CancellationToken cancellationToken = default);

    Task<bool> EliminarDocumentoPendienteAsync(
        string nombreUsuario,
        string fecha,
        string nombreArchivo,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConfiguracionProducto>> ListarConfiguracionesProductoAsync(
        CancellationToken cancellationToken = default);

    Task<ConfiguracionProducto?> ObtenerConfiguracionProductoAsync(
        string producto,
        CancellationToken cancellationToken = default);

    Task GuardarConfiguracionProductoAsync(
        ConfiguracionProducto configuracion,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsuarioEscaneoResumen>> ListarUsuariosConEscaneosAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FechaEscaneoResumen>> ListarFechasConTotalEscaneoAsync(
        string nombreUsuario,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentoProcesadoConsulta>> ListarDocumentosProcesadosAsync(
        string nombreUsuario,
        string fecha,
        CancellationToken cancellationToken = default);

    Task<int> ContarDocumentosEscaneadosAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FechaEscaneoResumen>> ListarEscaneosPorFechaAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsuarioEscaneoTotal>> ListarEscaneosPorUsuarioAsync(
        DateOnly? desde,
        DateOnly? hasta,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MesEscaneoResumen>> ListarEscaneosPorMesAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario = null,
        CancellationToken cancellationToken = default);

    Task<bool> ProbarConexionSqlAsync(
        string? connectionStringOverride = null,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<RadicaWebNotificacionConsulta> Items, int Total)> ListarRadicaWebNotificacionesAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default);

    Task<RadicaWebNotificacionConsulta?> ObtenerRadicaWebNotificacionAsync(
        long radicaWebApiId,
        CancellationToken cancellationToken = default);

    Task<bool> ActualizarRadicaWebNotificacionAsync(
        long radicaWebApiId,
        RadicaWebBusquedaResultado resultado,
        CancellationToken cancellationToken = default);

    Task<RadicaWebKpiResumen> ObtenerRadicaWebKpiResumenAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadicaWebUsuarioKpi>> ListarRadicaWebKpiPorUsuarioAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadicaWebBodegaKpi>> ListarRadicaWebKpiPorBodegaAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RadicaWebFechaFacturaKpi>> ListarRadicaWebKpiPorFechaFacturaAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> ListarUsuariosRadicaWebAsync(CancellationToken cancellationToken = default);
}

public class TrazabilidadConsultaSqlService : ITrazabilidadConsultaSqlService
{
    private readonly string _bootstrapConnectionString;
    private readonly ILogger<TrazabilidadConsultaSqlService> _logger;

    public TrazabilidadConsultaSqlService(
        IOptions<TrazabilidadSqlSettings> settings,
        ILogger<TrazabilidadConsultaSqlService> logger)
    {
        _bootstrapConnectionString = settings.Value.ConnectionString;
        _logger = logger;
    }

    public Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        const string script = """
IF DB_ID(N'Scaneados') IS NULL
BEGIN
    CREATE DATABASE Scaneados;
END

USE Scaneados;

IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Usuarios
    (
        UsuarioId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
        NombreUsuario nvarchar(100) NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Usuarios_FechaCreacion DEFAULT (sysdatetime()),
        CONSTRAINT UQ_Usuarios_NombreUsuario UNIQUE (NombreUsuario)
    );
END

IF OBJECT_ID(N'dbo.FechasProcesamiento', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FechasProcesamiento
    (
        FechaProcesamientoId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FechasProcesamiento PRIMARY KEY,
        UsuarioId int NOT NULL,
        FechaProcesamiento date NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_FechasProcesamiento_FechaCreacion DEFAULT (sysdatetime()),
        CONSTRAINT FK_FechasProcesamiento_Usuarios FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios(UsuarioId),
        CONSTRAINT UQ_FechasProcesamiento_Usuario_Fecha UNIQUE (UsuarioId, FechaProcesamiento)
    );
END

IF OBJECT_ID(N'dbo.DocumentosProcesados', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DocumentosProcesados
    (
        DocumentoProcesadoId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentosProcesados PRIMARY KEY,
        FechaProcesamientoId int NOT NULL,
        NombreArchivo nvarchar(260) NOT NULL,
        Soporte nvarchar(100) NULL,
        IdPaciente int NULL,
        IdBodega nvarchar(100) NULL,
        IdCartera nvarchar(100) NULL,
        FechaFactura datetime2(0) NULL,
        Procesado bit NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_DocumentosProcesados_FechaCreacion DEFAULT (sysdatetime()),
        CONSTRAINT FK_DocumentosProcesados_FechasProcesamiento FOREIGN KEY (FechaProcesamientoId) REFERENCES dbo.FechasProcesamiento(FechaProcesamientoId)
    );

    CREATE INDEX IX_DocumentosProcesados_FechaProcesamientoId
        ON dbo.DocumentosProcesados (FechaProcesamientoId);
END

IF COL_LENGTH(N'dbo.DocumentosProcesados', N'IdBodega') IS NULL
    ALTER TABLE dbo.DocumentosProcesados ADD IdBodega nvarchar(100) NULL;

IF COL_LENGTH(N'dbo.DocumentosProcesados', N'IdCartera') IS NULL
    ALTER TABLE dbo.DocumentosProcesados ADD IdCartera nvarchar(100) NULL;

IF COL_LENGTH(N'dbo.DocumentosProcesados', N'FechaFactura') IS NULL
    ALTER TABLE dbo.DocumentosProcesados ADD FechaFactura datetime2(0) NULL;

IF OBJECT_ID(N'dbo.Configuraciones', N'U') IS NOT NULL
AND COL_LENGTH(N'dbo.Configuraciones', N'Clave') IS NOT NULL
BEGIN
    DROP TABLE dbo.Configuraciones;
END

IF OBJECT_ID(N'dbo.Configuraciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Configuraciones
    (
        ConfiguracionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Configuraciones PRIMARY KEY,
        Producto nvarchar(100) NOT NULL,
        Endpoint nvarchar(MAX) NULL,
        EndpointVerificacion nvarchar(MAX) NULL,
        ClaveCredencial nvarchar(MAX) NULL,
        ValorAdicional nvarchar(MAX) NULL,
        Prompt nvarchar(MAX) NULL,
        Descripcion nvarchar(500) NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Configuraciones_FechaCreacion DEFAULT (sysdatetime()),
        FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_Configuraciones_FechaActualizacion DEFAULT (sysdatetime()),
        CONSTRAINT UQ_Configuraciones_Producto UNIQUE (Producto)
    );
END

;WITH Duplicados AS
(
    SELECT
        DocumentoProcesadoId,
        ROW_NUMBER() OVER
        (
            PARTITION BY FechaProcesamientoId, NombreArchivo
            ORDER BY
                CASE WHEN Procesado = 1 THEN 1 ELSE 0 END DESC,
                CASE WHEN Soporte IS NOT NULL THEN 1 ELSE 0 END DESC,
                CASE WHEN IdPaciente IS NOT NULL THEN 1 ELSE 0 END DESC,
                FechaCreacion DESC,
                DocumentoProcesadoId DESC
        ) AS RowNum
    FROM dbo.DocumentosProcesados
)
DELETE FROM dbo.DocumentosProcesados
WHERE DocumentoProcesadoId IN
(
    SELECT DocumentoProcesadoId
    FROM Duplicados
    WHERE RowNum > 1
);

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE name = N'UX_DocumentosProcesados_FechaProcesamientoId_NombreArchivo'
      AND object_id = OBJECT_ID(N'dbo.DocumentosProcesados')
)
BEGIN
    CREATE UNIQUE INDEX UX_DocumentosProcesados_FechaProcesamientoId_NombreArchivo
        ON dbo.DocumentosProcesados (FechaProcesamientoId, NombreArchivo);
END
""";

        return EjecutarNonQueryAsync(script, cancellationToken, databaseName: "master");
    }

    public async Task<bool> UsuarioExisteAsync(string nombreUsuario, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT TOP (1) 1
FROM dbo.Usuarios
WHERE NombreUsuario = @NombreUsuario;
""";

        return await EjecutarScalarAsync(
            sql,
            static async command =>
            {
                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value;
            },
            cancellationToken,
            command => command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim()));
    }

    public async Task<IReadOnlyList<string>> ListarFechasDisponiblesAsync(
        string nombreUsuario,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT CONVERT(varchar(10), fp.FechaProcesamiento, 23) AS Fecha
FROM dbo.FechasProcesamiento fp
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @NombreUsuario
ORDER BY fp.FechaProcesamiento DESC;
""";

        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                var fechas = new List<string>();
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    fechas.Add(reader.GetString(0));

                return (IReadOnlyList<string>)fechas;
            },
            cancellationToken,
            command => command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim()));
    }

    public async Task<IReadOnlyList<CalendarioDiaResumen>> ListarResumenCalendarioPorMesAsync(
        string nombreUsuario,
        int anio,
        int mes,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    CONVERT(varchar(10), fp.FechaProcesamiento, 23) AS Fecha,
    COUNT(dp.DocumentoProcesadoId) AS TotalEscaneados,
    SUM(CASE WHEN dp.Procesado = 0 THEN 1 ELSE 0 END) AS NoProcesados
FROM dbo.FechasProcesamiento fp
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
LEFT JOIN dbo.DocumentosProcesados dp ON dp.FechaProcesamientoId = fp.FechaProcesamientoId
WHERE u.NombreUsuario = @NombreUsuario
  AND YEAR(fp.FechaProcesamiento) = @Anio
  AND MONTH(fp.FechaProcesamiento) = @Mes
GROUP BY fp.FechaProcesamiento
ORDER BY fp.FechaProcesamiento;
""";

        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                var items = new List<CalendarioDiaResumen>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(new CalendarioDiaResumen
                    {
                        Fecha = reader.GetString(0),
                        TotalEscaneados = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        NoProcesados = reader.IsDBNull(2) ? 0 : reader.GetInt32(2)
                    });
                }

                return (IReadOnlyList<CalendarioDiaResumen>)items;
            },
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim());
                command.Parameters.AddWithValue("@Anio", anio);
                command.Parameters.AddWithValue("@Mes", mes);
            });
    }

    public async Task<IReadOnlyList<FechaEscaneoResumen>> ListarFechasConTotalEscaneoAsync(
        string nombreUsuario,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    CONVERT(varchar(10), fp.FechaProcesamiento, 23) AS Fecha,
    COUNT(dp.DocumentoProcesadoId) AS TotalEscaneo
FROM dbo.FechasProcesamiento fp
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
LEFT JOIN dbo.DocumentosProcesados dp ON dp.FechaProcesamientoId = fp.FechaProcesamientoId
WHERE u.NombreUsuario = @NombreUsuario
GROUP BY fp.FechaProcesamiento
ORDER BY fp.FechaProcesamiento DESC;
""";

        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                var fechas = new List<FechaEscaneoResumen>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    fechas.Add(new FechaEscaneoResumen
                    {
                        Fecha = reader.GetString(0),
                        TotalEscaneo = reader.GetInt32(1)
                    });
                }

                return (IReadOnlyList<FechaEscaneoResumen>)fechas;
            },
            cancellationToken,
            command => command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim()));
    }

    public async Task<bool> FechaExisteAsync(string nombreUsuario, string fecha, CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT TOP (1) 1
FROM dbo.FechasProcesamiento fp
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @NombreUsuario
  AND fp.FechaProcesamiento = @FechaProcesamiento;
""";

        var fechaDate = ParseFecha(fecha);
        return await EjecutarScalarAsync(
            sql,
            static async command =>
            {
                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value;
            },
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim());
                command.Parameters.Add("@FechaProcesamiento", System.Data.SqlDbType.Date).Value =
                    fechaDate.ToDateTime(TimeOnly.MinValue);
            });
    }

    public async Task<ResumenLogDiario?> ObtenerResumenAsync(
        string nombreUsuario,
        string fecha,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    SUM(CASE WHEN dp.Procesado = 1 THEN 1 ELSE 0 END) AS CantidadProcesados,
    SUM(CASE WHEN dp.Procesado = 0 THEN 1 ELSE 0 END) AS NoProcesados
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @NombreUsuario
  AND fp.FechaProcesamiento = @FechaProcesamiento;
""";

        var fechaDate = ParseFecha(fecha);
        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                await using var reader = await command.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    return null;

                return new ResumenLogDiario
                {
                    CantidadProcesados = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                    NoProcesados = reader.IsDBNull(1) ? 0 : reader.GetInt32(1)
                };
            },
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim());
                command.Parameters.Add("@FechaProcesamiento", System.Data.SqlDbType.Date).Value =
                    fechaDate.ToDateTime(TimeOnly.MinValue);
            });
    }

    public async Task<IReadOnlyList<DocumentoPendiente>> ListarDocumentosPendientesAsync(
        string nombreUsuario,
        string fecha,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    dp.NombreArchivo,
    dp.FechaFactura
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @NombreUsuario
  AND fp.FechaProcesamiento = @FechaProcesamiento
  AND dp.Procesado = 0
ORDER BY dp.NombreArchivo;
""";

        var fechaDate = ParseFecha(fecha);
        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                var documentos = new List<DocumentoPendiente>();
                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    documentos.Add(new DocumentoPendiente
                    {
                        NombreArchivo = reader.GetString(0),
                        FechaFactura = reader.IsDBNull(1) ? null : reader.GetDateTime(1)
                    });
                }

                return (IReadOnlyList<DocumentoPendiente>)documentos;
            },
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim());
                command.Parameters.Add("@FechaProcesamiento", System.Data.SqlDbType.Date).Value =
                    fechaDate.ToDateTime(TimeOnly.MinValue);
            });
    }

    public async Task<bool> DocumentoPendienteExisteAsync(
        string nombreUsuario,
        string fecha,
        string nombreArchivo,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT TOP (1) 1
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @NombreUsuario
  AND fp.FechaProcesamiento = @FechaProcesamiento
  AND dp.NombreArchivo = @NombreArchivo
  AND dp.Procesado = 0;
""";

        var fechaDate = ParseFecha(fecha);
        return await EjecutarScalarAsync(
            sql,
            static async command =>
            {
                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value;
            },
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim());
                command.Parameters.Add("@FechaProcesamiento", System.Data.SqlDbType.Date).Value =
                    fechaDate.ToDateTime(TimeOnly.MinValue);
                command.Parameters.AddWithValue("@NombreArchivo", nombreArchivo);
            });
    }

    public async Task<bool> MarcarDocumentoProcesadoAsync(
        string nombreUsuario,
        string fecha,
        string nombreArchivo,
        string? soporte,
        int? idPaciente,
        string? idBodega,
        string? idCartera,
        DateTime? fechaFactura,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
UPDATE dp
SET
    dp.Soporte = @Soporte,
    dp.IdPaciente = @IdPaciente,
    dp.IdBodega = @IdBodega,
    dp.IdCartera = @IdCartera,
    dp.FechaFactura = @FechaFactura,
    dp.Procesado = 1
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @NombreUsuario
  AND fp.FechaProcesamiento = @FechaProcesamiento
  AND dp.NombreArchivo = @NombreArchivo;
""";

        var fechaDate = ParseFecha(fecha);
        var affected = await EjecutarScalarAsync(
            sql,
            static async command => await command.ExecuteNonQueryAsync(),
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim());
                command.Parameters.Add("@FechaProcesamiento", System.Data.SqlDbType.Date).Value =
                    fechaDate.ToDateTime(TimeOnly.MinValue);
                command.Parameters.AddWithValue("@NombreArchivo", nombreArchivo);
                command.Parameters.Add("@Soporte", System.Data.SqlDbType.NVarChar, 100).Value =
                    (object?)soporte ?? DBNull.Value;
                command.Parameters.Add("@IdPaciente", System.Data.SqlDbType.Int).Value =
                    (object?)idPaciente ?? DBNull.Value;
                command.Parameters.Add("@IdBodega", System.Data.SqlDbType.NVarChar, 100).Value =
                    (object?)idBodega ?? DBNull.Value;
                command.Parameters.Add("@IdCartera", System.Data.SqlDbType.NVarChar, 100).Value =
                    (object?)idCartera ?? DBNull.Value;
                command.Parameters.Add("@FechaFactura", System.Data.SqlDbType.DateTime2).Value =
                    (object?)fechaFactura ?? DBNull.Value;
            });

        if (affected <= 0)
        {
            _logger.LogWarning(
                "TrazabilidadPortalDocumentoNoActualizado | Usuario={Usuario} | Fecha={Fecha} | Archivo={Archivo}",
                nombreUsuario,
                fecha,
                nombreArchivo);
        }

        return affected > 0;
    }

    public async Task<bool> EliminarDocumentoPendienteAsync(
        string nombreUsuario,
        string fecha,
        string nombreArchivo,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
DELETE dp
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @NombreUsuario
  AND fp.FechaProcesamiento = @FechaProcesamiento
  AND dp.NombreArchivo = @NombreArchivo
  AND dp.Procesado = 0;
""";

        var fechaDate = ParseFecha(fecha);
        var affected = await EjecutarScalarAsync(
            sql,
            static async command => await command.ExecuteNonQueryAsync(),
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim());
                command.Parameters.Add("@FechaProcesamiento", System.Data.SqlDbType.Date).Value =
                    fechaDate.ToDateTime(TimeOnly.MinValue);
                command.Parameters.AddWithValue("@NombreArchivo", nombreArchivo);
            });

        return affected > 0;
    }

    public async Task<IReadOnlyList<ConfiguracionProducto>> ListarConfiguracionesProductoAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    ConfiguracionId,
    Producto,
    Endpoint,
    EndpointVerificacion,
    ClaveCredencial,
    ValorAdicional,
    Prompt,
    Descripcion,
    FechaCreacion,
    FechaActualizacion
FROM dbo.Configuraciones
ORDER BY Producto;
""";

        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                var lista = new List<ConfiguracionProducto>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    lista.Add(LeerConfiguracionProducto(reader));

                return (IReadOnlyList<ConfiguracionProducto>)lista;
            },
            cancellationToken,
            connectionStringOverride: _bootstrapConnectionString);
    }

    public Task<ConfiguracionProducto?> ObtenerConfiguracionProductoAsync(
        string producto,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT TOP (1)
    ConfiguracionId,
    Producto,
    Endpoint,
    EndpointVerificacion,
    ClaveCredencial,
    ValorAdicional,
    Prompt,
    Descripcion,
    FechaCreacion,
    FechaActualizacion
FROM dbo.Configuraciones
WHERE Producto = @Producto;
""";

        return EjecutarReaderAsync(
            sql,
            async command =>
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                return await reader.ReadAsync(cancellationToken)
                    ? LeerConfiguracionProducto(reader)
                    : null;
            },
            cancellationToken,
            command => command.Parameters.AddWithValue("@Producto", producto.Trim()),
            connectionStringOverride: _bootstrapConnectionString);
    }

    public async Task GuardarConfiguracionProductoAsync(
        ConfiguracionProducto configuracion,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
IF EXISTS (SELECT 1 FROM dbo.Configuraciones WHERE Producto = @Producto)
BEGIN
    UPDATE dbo.Configuraciones
    SET
        Endpoint = @Endpoint,
        EndpointVerificacion = @EndpointVerificacion,
        ClaveCredencial = @ClaveCredencial,
        ValorAdicional = @ValorAdicional,
        Prompt = @Prompt,
        Descripcion = @Descripcion,
        FechaActualizacion = SYSDATETIME()
    WHERE Producto = @Producto;
END
ELSE
BEGIN
    INSERT INTO dbo.Configuraciones
    (
        Producto,
        Endpoint,
        EndpointVerificacion,
        ClaveCredencial,
        ValorAdicional,
        Prompt,
        Descripcion
    )
    VALUES
    (
        @Producto,
        @Endpoint,
        @EndpointVerificacion,
        @ClaveCredencial,
        @ValorAdicional,
        @Prompt,
        @Descripcion
    );
END
""";

        await EjecutarNonQueryAsync(
            sql,
            cancellationToken,
            command => AgregarParametrosConfiguracionProducto(command, configuracion),
            connectionStringOverride: _bootstrapConnectionString);
    }

    private static ConfiguracionProducto LeerConfiguracionProducto(SqlDataReader reader) =>
        new()
        {
            ConfiguracionId = reader.GetInt32(0),
            Producto = reader.GetString(1),
            Endpoint = reader.IsDBNull(2) ? null : reader.GetString(2),
            EndpointVerificacion = reader.IsDBNull(3) ? null : reader.GetString(3),
            ClaveCredencial = reader.IsDBNull(4) ? null : reader.GetString(4),
            ValorAdicional = reader.IsDBNull(5) ? null : reader.GetString(5),
            Prompt = reader.IsDBNull(6) ? null : reader.GetString(6),
            Descripcion = reader.IsDBNull(7) ? null : reader.GetString(7),
            FechaCreacion = reader.GetDateTime(8),
            FechaActualizacion = reader.GetDateTime(9)
        };

    private static void AgregarParametrosConfiguracionProducto(SqlCommand command, ConfiguracionProducto configuracion)
    {
        command.Parameters.AddWithValue("@Producto", configuracion.Producto.Trim());
        command.Parameters.Add("@Endpoint", System.Data.SqlDbType.NVarChar, -1).Value =
            (object?)configuracion.Endpoint ?? DBNull.Value;
        command.Parameters.Add("@EndpointVerificacion", System.Data.SqlDbType.NVarChar, -1).Value =
            (object?)configuracion.EndpointVerificacion ?? DBNull.Value;
        command.Parameters.Add("@ClaveCredencial", System.Data.SqlDbType.NVarChar, -1).Value =
            (object?)configuracion.ClaveCredencial ?? DBNull.Value;
        command.Parameters.Add("@ValorAdicional", System.Data.SqlDbType.NVarChar, -1).Value =
            (object?)configuracion.ValorAdicional ?? DBNull.Value;
        command.Parameters.Add("@Prompt", System.Data.SqlDbType.NVarChar, -1).Value =
            (object?)configuracion.Prompt ?? DBNull.Value;
        command.Parameters.Add("@Descripcion", System.Data.SqlDbType.NVarChar, 500).Value =
            (object?)configuracion.Descripcion ?? DBNull.Value;
    }

    public async Task<IReadOnlyList<UsuarioEscaneoResumen>> ListarUsuariosConEscaneosAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    u.NombreUsuario,
    COUNT(fp.FechaProcesamientoId) AS CantidadDiasEscaneados,
    CONVERT(varchar(10), MAX(fp.FechaProcesamiento), 23) AS UltimoDiaEscaneado
FROM dbo.Usuarios u
INNER JOIN dbo.FechasProcesamiento fp ON fp.UsuarioId = u.UsuarioId
GROUP BY u.NombreUsuario
ORDER BY u.NombreUsuario;
""";

        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                var usuarios = new List<UsuarioEscaneoResumen>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    usuarios.Add(new UsuarioEscaneoResumen
                    {
                        NombreUsuario = reader.GetString(0),
                        CantidadDiasEscaneados = reader.GetInt32(1),
                        UltimoDiaEscaneado = reader.GetString(2)
                    });
                }

                return (IReadOnlyList<UsuarioEscaneoResumen>)usuarios;
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentoProcesadoConsulta>> ListarDocumentosProcesadosAsync(
        string nombreUsuario,
        string fecha,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    dp.DocumentoProcesadoId,
    dp.NombreArchivo,
    dp.Soporte,
    dp.IdPaciente,
    dp.IdBodega,
    dp.IdCartera,
    dp.FechaFactura,
    dp.Procesado,
    dp.FechaCreacion
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @NombreUsuario
  AND fp.FechaProcesamiento = @FechaProcesamiento
ORDER BY dp.NombreArchivo;
""";

        var fechaDate = ParseFecha(fecha);
        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                var documentos = new List<DocumentoProcesadoConsulta>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    documentos.Add(new DocumentoProcesadoConsulta
                    {
                        DocumentoProcesadoId = reader.GetInt64(0),
                        NombreArchivo = reader.GetString(1),
                        Soporte = reader.IsDBNull(2) ? null : reader.GetString(2),
                        IdPaciente = reader.IsDBNull(3) ? null : reader.GetInt32(3),
                        IdBodega = reader.IsDBNull(4) ? null : reader.GetString(4),
                        IdCartera = reader.IsDBNull(5) ? null : reader.GetString(5),
                        FechaFactura = reader.IsDBNull(6) ? null : reader.GetDateTime(6),
                        Procesado = !reader.IsDBNull(7) && reader.GetBoolean(7),
                        FechaCreacion = reader.GetDateTime(8)
                    });
                }

                return (IReadOnlyList<DocumentoProcesadoConsulta>)documentos;
            },
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim());
                command.Parameters.Add("@FechaProcesamiento", System.Data.SqlDbType.Date).Value =
                    fechaDate.ToDateTime(TimeOnly.MinValue);
            });
    }

    public async Task<int> ContarDocumentosEscaneadosAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT COUNT(dp.DocumentoProcesadoId)
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE (@Desde IS NULL OR fp.FechaProcesamiento >= @Desde)
  AND (@Hasta IS NULL OR fp.FechaProcesamiento <= @Hasta)
  AND (@NombreUsuario IS NULL OR u.NombreUsuario = @NombreUsuario);
""";

        return await EjecutarScalarAsync(
            sql,
            async command =>
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                await reader.ReadAsync(cancellationToken);
                return reader.GetInt32(0);
            },
            cancellationToken,
            command => AgregarFiltrosInforme(command, desde, hasta, nombreUsuario));
    }

    public async Task<IReadOnlyList<FechaEscaneoResumen>> ListarEscaneosPorFechaAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    CONVERT(varchar(10), fp.FechaProcesamiento, 23) AS Fecha,
    COUNT(dp.DocumentoProcesadoId) AS TotalEscaneo
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE (@Desde IS NULL OR fp.FechaProcesamiento >= @Desde)
  AND (@Hasta IS NULL OR fp.FechaProcesamiento <= @Hasta)
  AND (@NombreUsuario IS NULL OR u.NombreUsuario = @NombreUsuario)
GROUP BY fp.FechaProcesamiento
ORDER BY fp.FechaProcesamiento DESC;
""";

        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                var items = new List<FechaEscaneoResumen>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(new FechaEscaneoResumen
                    {
                        Fecha = reader.GetString(0),
                        TotalEscaneo = reader.GetInt32(1)
                    });
                }

                return (IReadOnlyList<FechaEscaneoResumen>)items;
            },
            cancellationToken,
            command => AgregarFiltrosInforme(command, desde, hasta, nombreUsuario));
    }

    public async Task<IReadOnlyList<UsuarioEscaneoTotal>> ListarEscaneosPorUsuarioAsync(
        DateOnly? desde,
        DateOnly? hasta,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    u.NombreUsuario,
    COUNT(dp.DocumentoProcesadoId) AS TotalEscaneo
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE (@Desde IS NULL OR fp.FechaProcesamiento >= @Desde)
  AND (@Hasta IS NULL OR fp.FechaProcesamiento <= @Hasta)
GROUP BY u.NombreUsuario
ORDER BY u.NombreUsuario;
""";

        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                var items = new List<UsuarioEscaneoTotal>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(new UsuarioEscaneoTotal
                    {
                        NombreUsuario = reader.GetString(0),
                        TotalEscaneo = reader.GetInt32(1)
                    });
                }

                return (IReadOnlyList<UsuarioEscaneoTotal>)items;
            },
            cancellationToken,
            command => AgregarFiltrosInforme(command, desde, hasta));
    }

    public async Task<IReadOnlyList<MesEscaneoResumen>> ListarEscaneosPorMesAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    FORMAT(fp.FechaProcesamiento, 'yyyy-MM') AS Mes,
    COUNT(dp.DocumentoProcesadoId) AS TotalEscaneo
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE (@Desde IS NULL OR fp.FechaProcesamiento >= @Desde)
  AND (@Hasta IS NULL OR fp.FechaProcesamiento <= @Hasta)
  AND (@NombreUsuario IS NULL OR u.NombreUsuario = @NombreUsuario)
GROUP BY YEAR(fp.FechaProcesamiento), MONTH(fp.FechaProcesamiento), FORMAT(fp.FechaProcesamiento, 'yyyy-MM')
ORDER BY YEAR(fp.FechaProcesamiento) DESC, MONTH(fp.FechaProcesamiento) DESC;
""";

        return await EjecutarReaderAsync(
            sql,
            async command =>
            {
                var items = new List<MesEscaneoResumen>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(new MesEscaneoResumen
                    {
                        Mes = reader.GetString(0),
                        TotalEscaneo = reader.GetInt32(1)
                    });
                }

                return (IReadOnlyList<MesEscaneoResumen>)items;
            },
            cancellationToken,
            command => AgregarFiltrosInforme(command, desde, hasta, nombreUsuario));
    }

    public async Task<(IReadOnlyList<RadicaWebNotificacionConsulta> Items, int Total)> ListarRadicaWebNotificacionesAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success,
        int pagina,
        int tamanoPagina,
        CancellationToken cancellationToken = default)
    {
        pagina = Math.Max(1, pagina);
        tamanoPagina = Math.Clamp(tamanoPagina, 5, 100);
        var offset = (pagina - 1) * tamanoPagina;

        const string countSql = """
SELECT COUNT(*)
FROM dbo.RadicaWebAPI rw
INNER JOIN dbo.Usuarios u ON u.UsuarioId = rw.UsuarioId
WHERE (@Desde IS NULL OR CAST(rw.CreadoEn AS date) >= @Desde)
  AND (@Hasta IS NULL OR CAST(rw.CreadoEn AS date) <= @Hasta)
  AND (@NombreUsuario IS NULL OR u.NombreUsuario = @NombreUsuario)
  AND (@Bodega IS NULL OR rw.Bodega LIKE '%' + @Bodega + '%')
  AND (@Success IS NULL OR rw.Success = @Success);
""";

        const string listSql = """
SELECT
    rw.RadicaWebApiId,
    u.NombreUsuario,
    rw.FechaFactura,
    rw.Bodega,
    rw.Success,
    rw.Message,
    rw.SolicitudId,
    rw.RegistrosInsertados,
    rw.TotalRegistros,
    rw.JobId,
    rw.StatusCode,
    rw.Error,
    rw.Timestamp,
    rw.Path,
    rw.CreadoEn
FROM dbo.RadicaWebAPI rw
INNER JOIN dbo.Usuarios u ON u.UsuarioId = rw.UsuarioId
WHERE (@Desde IS NULL OR CAST(rw.CreadoEn AS date) >= @Desde)
  AND (@Hasta IS NULL OR CAST(rw.CreadoEn AS date) <= @Hasta)
  AND (@NombreUsuario IS NULL OR u.NombreUsuario = @NombreUsuario)
  AND (@Bodega IS NULL OR rw.Bodega LIKE '%' + @Bodega + '%')
  AND (@Success IS NULL OR rw.Success = @Success)
ORDER BY rw.CreadoEn DESC, rw.RadicaWebApiId DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
""";

        void ConfigurarFiltros(SqlCommand command)
        {
            AgregarFiltrosRadicaWeb(command, desde, hasta, nombreUsuario, bodega, success);
            command.Parameters.AddWithValue("@Offset", offset);
            command.Parameters.AddWithValue("@PageSize", tamanoPagina);
        }

        var total = await EjecutarScalarAsync(
            countSql,
            async command => Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)),
            cancellationToken,
            ConfigurarFiltros);

        var items = await EjecutarReaderAsync(
            listSql,
            async command =>
            {
                var lista = new List<RadicaWebNotificacionConsulta>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    lista.Add(LeerRadicaWebNotificacion(reader));

                return (IReadOnlyList<RadicaWebNotificacionConsulta>)lista;
            },
            cancellationToken,
            ConfigurarFiltros);

        return (items, total);
    }

    public Task<RadicaWebNotificacionConsulta?> ObtenerRadicaWebNotificacionAsync(
        long radicaWebApiId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    rw.RadicaWebApiId,
    u.NombreUsuario,
    rw.FechaFactura,
    rw.Bodega,
    rw.Success,
    rw.Message,
    rw.SolicitudId,
    rw.RegistrosInsertados,
    rw.TotalRegistros,
    rw.JobId,
    rw.StatusCode,
    rw.Error,
    rw.Timestamp,
    rw.Path,
    rw.CreadoEn
FROM dbo.RadicaWebAPI rw
INNER JOIN dbo.Usuarios u ON u.UsuarioId = rw.UsuarioId
WHERE rw.RadicaWebApiId = @RadicaWebApiId;
""";

        return EjecutarReaderAsync(
            sql,
            async command =>
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                    return null;

                return LeerRadicaWebNotificacion(reader);
            },
            cancellationToken,
            command => command.Parameters.AddWithValue("@RadicaWebApiId", radicaWebApiId));
    }

    public async Task<bool> ActualizarRadicaWebNotificacionAsync(
        long radicaWebApiId,
        RadicaWebBusquedaResultado resultado,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
UPDATE dbo.RadicaWebAPI
SET
    Success = @Success,
    Message = @Message,
    SolicitudId = @SolicitudId,
    RegistrosInsertados = @RegistrosInsertados,
    TotalRegistros = @TotalRegistros,
    JobId = @JobId,
    StatusCode = @StatusCode,
    Error = @Error,
    Timestamp = @Timestamp,
    Path = @Path
WHERE RadicaWebApiId = @RadicaWebApiId;
""";

        await EjecutarNonQueryAsync(
            sql,
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("@RadicaWebApiId", radicaWebApiId);
                command.Parameters.Add("@Success", System.Data.SqlDbType.Bit).Value =
                    (object?)resultado.Success ?? DBNull.Value;
                command.Parameters.Add("@Message", System.Data.SqlDbType.NVarChar, -1).Value =
                    (object?)resultado.Message ?? DBNull.Value;
                command.Parameters.Add("@SolicitudId", System.Data.SqlDbType.Int).Value =
                    (object?)resultado.SolicitudId ?? DBNull.Value;
                command.Parameters.Add("@RegistrosInsertados", System.Data.SqlDbType.Int).Value =
                    (object?)resultado.RegistrosInsertados ?? DBNull.Value;
                command.Parameters.Add("@TotalRegistros", System.Data.SqlDbType.Int).Value =
                    (object?)resultado.TotalRegistros ?? DBNull.Value;
                command.Parameters.Add("@JobId", System.Data.SqlDbType.NVarChar, 200).Value =
                    (object?)resultado.JobId ?? DBNull.Value;
                command.Parameters.Add("@StatusCode", System.Data.SqlDbType.Int).Value =
                    (object?)resultado.HttpStatusCode ?? DBNull.Value;
                command.Parameters.Add("@Error", System.Data.SqlDbType.NVarChar, 200).Value =
                    (object?)resultado.Error ?? DBNull.Value;
                command.Parameters.Add("@Timestamp", System.Data.SqlDbType.DateTimeOffset).Value =
                    (object?)resultado.Timestamp ?? DBNull.Value;
                command.Parameters.Add("@Path", System.Data.SqlDbType.NVarChar, 500).Value =
                    (object?)resultado.Path ?? DBNull.Value;
            });

        return true;
    }

    public Task<RadicaWebKpiResumen> ObtenerRadicaWebKpiResumenAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    COUNT(*) AS Total,
    SUM(CASE WHEN rw.Success = 1 THEN 1 ELSE 0 END) AS Exitosas,
    SUM(CASE WHEN rw.Success = 0 THEN 1 ELSE 0 END) AS Fallidas,
    SUM(CASE WHEN rw.Success IS NULL THEN 1 ELSE 0 END) AS SinResultado,
    COUNT(DISTINCT u.NombreUsuario) AS UsuariosDistintos,
    COUNT(DISTINCT rw.Bodega) AS BodegasDistintas
FROM dbo.RadicaWebAPI rw
INNER JOIN dbo.Usuarios u ON u.UsuarioId = rw.UsuarioId
WHERE (@Desde IS NULL OR CAST(rw.CreadoEn AS date) >= @Desde)
  AND (@Hasta IS NULL OR CAST(rw.CreadoEn AS date) <= @Hasta)
  AND (@NombreUsuario IS NULL OR u.NombreUsuario = @NombreUsuario)
  AND (@Bodega IS NULL OR rw.Bodega LIKE '%' + @Bodega + '%')
  AND (@Success IS NULL OR rw.Success = @Success);
""";

        return EjecutarReaderAsync(
            sql,
            async command =>
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return new RadicaWebKpiResumen();
                }

                return new RadicaWebKpiResumen
                {
                    Total = reader.GetInt32(0),
                    Exitosas = reader.GetInt32(1),
                    Fallidas = reader.GetInt32(2),
                    SinResultado = reader.GetInt32(3),
                    UsuariosDistintos = reader.GetInt32(4),
                    BodegasDistintas = reader.GetInt32(5)
                };
            },
            cancellationToken,
            command => AgregarFiltrosRadicaWeb(command, desde, hasta, nombreUsuario, bodega, success));
    }

    public Task<IReadOnlyList<RadicaWebUsuarioKpi>> ListarRadicaWebKpiPorUsuarioAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    u.NombreUsuario,
    COUNT(*) AS Total,
    SUM(CASE WHEN rw.Success = 1 THEN 1 ELSE 0 END) AS Exitosas,
    SUM(CASE WHEN rw.Success = 0 THEN 1 ELSE 0 END) AS Fallidas,
    SUM(CASE WHEN rw.Success IS NULL OR rw.Success = 0 THEN 1 ELSE 0 END) AS SinNotificar
FROM dbo.RadicaWebAPI rw
INNER JOIN dbo.Usuarios u ON u.UsuarioId = rw.UsuarioId
WHERE (@Desde IS NULL OR CAST(rw.CreadoEn AS date) >= @Desde)
  AND (@Hasta IS NULL OR CAST(rw.CreadoEn AS date) <= @Hasta)
  AND (@NombreUsuario IS NULL OR u.NombreUsuario = @NombreUsuario)
  AND (@Bodega IS NULL OR rw.Bodega LIKE '%' + @Bodega + '%')
  AND (@Success IS NULL OR rw.Success = @Success)
GROUP BY u.NombreUsuario
ORDER BY COUNT(*) DESC, u.NombreUsuario;
""";

        return EjecutarReaderAsync(
            sql,
            async command =>
            {
                var items = new List<RadicaWebUsuarioKpi>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(new RadicaWebUsuarioKpi
                    {
                        NombreUsuario = reader.GetString(0),
                        Total = reader.GetInt32(1),
                        Exitosas = reader.GetInt32(2),
                        Fallidas = reader.GetInt32(3),
                        SinNotificar = reader.GetInt32(4)
                    });
                }

                return (IReadOnlyList<RadicaWebUsuarioKpi>)items;
            },
            cancellationToken,
            command => AgregarFiltrosRadicaWeb(command, desde, hasta, nombreUsuario, bodega, success));
    }

    public Task<IReadOnlyList<RadicaWebBodegaKpi>> ListarRadicaWebKpiPorBodegaAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    rw.Bodega,
    COUNT(*) AS Total,
    SUM(CASE WHEN rw.Success = 1 THEN 1 ELSE 0 END) AS Exitosas,
    SUM(CASE WHEN rw.Success = 0 OR rw.Success IS NULL THEN 1 ELSE 0 END) AS Fallidas
FROM dbo.RadicaWebAPI rw
INNER JOIN dbo.Usuarios u ON u.UsuarioId = rw.UsuarioId
WHERE (@Desde IS NULL OR CAST(rw.CreadoEn AS date) >= @Desde)
  AND (@Hasta IS NULL OR CAST(rw.CreadoEn AS date) <= @Hasta)
  AND (@NombreUsuario IS NULL OR u.NombreUsuario = @NombreUsuario)
  AND (@Bodega IS NULL OR rw.Bodega LIKE '%' + @Bodega + '%')
  AND (@Success IS NULL OR rw.Success = @Success)
GROUP BY rw.Bodega
ORDER BY COUNT(*) DESC, rw.Bodega;
""";

        return EjecutarReaderAsync(
            sql,
            async command =>
            {
                var items = new List<RadicaWebBodegaKpi>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(new RadicaWebBodegaKpi
                    {
                        Bodega = reader.GetString(0),
                        Total = reader.GetInt32(1),
                        Exitosas = reader.GetInt32(2),
                        Fallidas = reader.GetInt32(3)
                    });
                }

                return (IReadOnlyList<RadicaWebBodegaKpi>)items;
            },
            cancellationToken,
            command => AgregarFiltrosRadicaWeb(command, desde, hasta, nombreUsuario, bodega, success));
    }

    public Task<IReadOnlyList<RadicaWebFechaFacturaKpi>> ListarRadicaWebKpiPorFechaFacturaAsync(
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    rw.FechaFactura,
    COUNT(*) AS Total,
    SUM(CASE WHEN rw.Success = 1 THEN 1 ELSE 0 END) AS Exitosas,
    SUM(CASE WHEN rw.Success = 0 OR rw.Success IS NULL THEN 1 ELSE 0 END) AS Fallidas
FROM dbo.RadicaWebAPI rw
INNER JOIN dbo.Usuarios u ON u.UsuarioId = rw.UsuarioId
WHERE (@Desde IS NULL OR CAST(rw.CreadoEn AS date) >= @Desde)
  AND (@Hasta IS NULL OR CAST(rw.CreadoEn AS date) <= @Hasta)
  AND (@NombreUsuario IS NULL OR u.NombreUsuario = @NombreUsuario)
  AND (@Bodega IS NULL OR rw.Bodega LIKE '%' + @Bodega + '%')
  AND (@Success IS NULL OR rw.Success = @Success)
GROUP BY rw.FechaFactura
ORDER BY rw.FechaFactura DESC;
""";

        return EjecutarReaderAsync(
            sql,
            async command =>
            {
                var items = new List<RadicaWebFechaFacturaKpi>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    items.Add(new RadicaWebFechaFacturaKpi
                    {
                        FechaFactura = DateOnly.FromDateTime(reader.GetDateTime(0)),
                        Total = reader.GetInt32(1),
                        Exitosas = reader.GetInt32(2),
                        Fallidas = reader.GetInt32(3)
                    });
                }

                return (IReadOnlyList<RadicaWebFechaFacturaKpi>)items;
            },
            cancellationToken,
            command => AgregarFiltrosRadicaWeb(command, desde, hasta, nombreUsuario, bodega, success));
    }

    public Task<IReadOnlyList<string>> ListarUsuariosRadicaWebAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT DISTINCT u.NombreUsuario
FROM dbo.RadicaWebAPI rw
INNER JOIN dbo.Usuarios u ON u.UsuarioId = rw.UsuarioId
ORDER BY u.NombreUsuario;
""";

        return EjecutarReaderAsync(
            sql,
            async command =>
            {
                var items = new List<string>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                    items.Add(reader.GetString(0));

                return (IReadOnlyList<string>)items;
            },
            cancellationToken);
    }

    public async Task<bool> ProbarConexionSqlAsync(
        string? connectionStringOverride = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var connectionString = string.IsNullOrWhiteSpace(connectionStringOverride)
                ? await ResolveOperationalConnectionStringAsync(cancellationToken)
                : connectionStringOverride;

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT 1";
            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result) == 1;
        }
        catch
        {
            return false;
        }
    }

    private static void AgregarFiltrosInforme(
        SqlCommand command,
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario = null)
    {
        command.Parameters.Add("@Desde", System.Data.SqlDbType.Date).Value =
            desde.HasValue ? desde.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
        command.Parameters.Add("@Hasta", System.Data.SqlDbType.Date).Value =
            hasta.HasValue ? hasta.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;

        if (nombreUsuario is null)
            command.Parameters.AddWithValue("@NombreUsuario", DBNull.Value);
        else
            command.Parameters.AddWithValue("@NombreUsuario", nombreUsuario.Trim());
    }

    private static void AgregarFiltrosRadicaWeb(
        SqlCommand command,
        DateOnly? desde,
        DateOnly? hasta,
        string? nombreUsuario,
        string? bodega,
        bool? success)
    {
        command.Parameters.Add("@Desde", System.Data.SqlDbType.Date).Value =
            desde.HasValue ? desde.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
        command.Parameters.Add("@Hasta", System.Data.SqlDbType.Date).Value =
            hasta.HasValue ? hasta.Value.ToDateTime(TimeOnly.MinValue) : DBNull.Value;
        command.Parameters.AddWithValue("@NombreUsuario",
            string.IsNullOrWhiteSpace(nombreUsuario) ? DBNull.Value : nombreUsuario.Trim());
        command.Parameters.AddWithValue("@Bodega",
            string.IsNullOrWhiteSpace(bodega) ? DBNull.Value : bodega.Trim());
        command.Parameters.Add("@Success", System.Data.SqlDbType.Bit).Value =
            success.HasValue ? success.Value : DBNull.Value;
    }

    private static RadicaWebNotificacionConsulta LeerRadicaWebNotificacion(SqlDataReader reader) =>
        new()
        {
            RadicaWebApiId = reader.GetInt64(0),
            NombreUsuario = reader.GetString(1),
            FechaFactura = DateOnly.FromDateTime(reader.GetDateTime(2)),
            Bodega = reader.GetString(3),
            Success = reader.IsDBNull(4) ? null : reader.GetBoolean(4),
            Message = reader.IsDBNull(5) ? null : reader.GetString(5),
            SolicitudId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            RegistrosInsertados = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            TotalRegistros = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            JobId = reader.IsDBNull(9) ? null : reader.GetString(9),
            StatusCode = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            Error = reader.IsDBNull(11) ? null : reader.GetString(11),
            Timestamp = reader.IsDBNull(12) ? null : reader.GetFieldValue<DateTimeOffset>(12),
            Path = reader.IsDBNull(13) ? null : reader.GetString(13),
            CreadoEn = reader.GetDateTime(14)
        };

    private async Task<string> ResolveOperationalConnectionStringAsync(CancellationToken cancellationToken) =>
        _bootstrapConnectionString;

    private async Task EjecutarNonQueryAsync(
        string sql,
        CancellationToken cancellationToken,
        Action<SqlCommand>? configure = null,
        string? databaseName = null,
        string? connectionStringOverride = null)
    {
        var baseConnectionString = connectionStringOverride
            ?? await ResolveOperationalConnectionStringAsync(cancellationToken);
        var connectionString = BuildConnectionString(databaseName, baseConnectionString);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        configure?.Invoke(command);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<T> EjecutarScalarAsync<T>(
        string sql,
        Func<SqlCommand, Task<T>> execute,
        CancellationToken cancellationToken,
        Action<SqlCommand>? configure = null,
        string? databaseName = null,
        string? connectionStringOverride = null)
    {
        var baseConnectionString = connectionStringOverride
            ?? await ResolveOperationalConnectionStringAsync(cancellationToken);
        var connectionString = BuildConnectionString(databaseName, baseConnectionString);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        configure?.Invoke(command);
        return await execute(command);
    }

    private async Task<T> EjecutarReaderAsync<T>(
        string sql,
        Func<SqlCommand, Task<T>> execute,
        CancellationToken cancellationToken,
        Action<SqlCommand>? configure = null,
        string? databaseName = null,
        string? connectionStringOverride = null)
    {
        var baseConnectionString = connectionStringOverride
            ?? await ResolveOperationalConnectionStringAsync(cancellationToken);
        var connectionString = BuildConnectionString(databaseName, baseConnectionString);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        configure?.Invoke(command);
        return await execute(command);
    }

    private static string BuildConnectionString(string? databaseName, string baseConnectionString)
    {
        return string.IsNullOrWhiteSpace(databaseName)
            ? baseConnectionString
            : new SqlConnectionStringBuilder(baseConnectionString) { InitialCatalog = databaseName }.ConnectionString;
    }

    private static DateOnly ParseFecha(string fecha) =>
        DateOnly.ParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
