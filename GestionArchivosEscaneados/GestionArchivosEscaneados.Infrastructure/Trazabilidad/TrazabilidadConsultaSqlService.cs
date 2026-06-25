using System.Globalization;
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

    Task<string?> ObtenerConfiguracionAsync(
        string clave,
        CancellationToken cancellationToken = default);

    Task<bool> GuardarConfiguracionAsync(
        string clave,
        string valor,
        string? descripcion = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UsuarioEscaneoResumen>> ListarUsuariosConEscaneosAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FechaEscaneoResumen>> ListarFechasConTotalEscaneoAsync(
        string nombreUsuario,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentoProcesadoConsulta>> ListarDocumentosProcesadosAsync(
        string nombreUsuario,
        string fecha,
        CancellationToken cancellationToken = default);
}

public class TrazabilidadConsultaSqlService : ITrazabilidadConsultaSqlService
{
    private readonly string _connectionString;
    private readonly ILogger<TrazabilidadConsultaSqlService> _logger;

    public TrazabilidadConsultaSqlService(
        IOptions<TrazabilidadSqlSettings> settings,
        ILogger<TrazabilidadConsultaSqlService> logger)
    {
        _connectionString = settings.Value.ConnectionString;
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

IF OBJECT_ID(N'dbo.Configuraciones', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Configuraciones
    (
        ConfiguracionId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Configuraciones PRIMARY KEY,
        Clave nvarchar(100) NOT NULL,
        Valor nvarchar(MAX) NOT NULL,
        Descripcion nvarchar(500) NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Configuraciones_FechaCreacion DEFAULT (sysdatetime()),
        FechaActualizacion datetime2(0) NOT NULL CONSTRAINT DF_Configuraciones_FechaActualizacion DEFAULT (sysdatetime()),
        CONSTRAINT UQ_Configuraciones_Clave UNIQUE (Clave)
    );

    CREATE INDEX IX_Configuraciones_Clave ON dbo.Configuraciones (Clave);
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

    public async Task<string?> ObtenerConfiguracionAsync(
        string clave,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT TOP (1) Valor
FROM dbo.Configuraciones
WHERE Clave = @Clave;
""";

        return await EjecutarScalarAsync(
            sql,
            static async command =>
            {
                var result = await command.ExecuteScalarAsync();
                return result != null && result != DBNull.Value ? result.ToString() : null;
            },
            cancellationToken,
            command => command.Parameters.AddWithValue("@Clave", clave.Trim()));
    }

    public async Task<bool> GuardarConfiguracionAsync(
        string clave,
        string valor,
        string? descripcion = null,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
IF EXISTS (SELECT 1 FROM dbo.Configuraciones WHERE Clave = @Clave)
BEGIN
    UPDATE dbo.Configuraciones
    SET Valor = @Valor, Descripcion = @Descripcion, FechaActualizacion = SYSDATETIME()
    WHERE Clave = @Clave;
END
ELSE
BEGIN
    INSERT INTO dbo.Configuraciones (Clave, Valor, Descripcion)
    VALUES (@Clave, @Valor, @Descripcion);
END
""";

        await EjecutarNonQueryAsync(
            sql,
            cancellationToken,
            command =>
            {
                command.Parameters.AddWithValue("@Clave", clave.Trim());
                command.Parameters.AddWithValue("@Valor", valor ?? string.Empty);
                command.Parameters.Add("@Descripcion", System.Data.SqlDbType.NVarChar, 500).Value =
                    (object?)descripcion ?? DBNull.Value;
            });

        return true;
    }

    public async Task<IReadOnlyList<UsuarioEscaneoResumen>> ListarUsuariosConEscaneosAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT
    u.NombreUsuario,
    COUNT(fp.FechaProcesamientoId) AS CantidadDiasEscaneados
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
                        CantidadDiasEscaneados = reader.GetInt32(1)
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

    private async Task EjecutarNonQueryAsync(
        string sql,
        CancellationToken cancellationToken,
        Action<SqlCommand>? configure = null,
        string? databaseName = null)
    {
        var connectionString = BuildConnectionString(databaseName);
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
        string? databaseName = null)
    {
        var connectionString = BuildConnectionString(databaseName);
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
        string? databaseName = null)
    {
        var connectionString = BuildConnectionString(databaseName);
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        configure?.Invoke(command);
        return await execute(command);
    }

    private string BuildConnectionString(string? databaseName)
    {
        return string.IsNullOrWhiteSpace(databaseName)
            ? _connectionString
            : new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = databaseName }.ConnectionString;
    }

    private static DateOnly ParseFecha(string fecha) =>
        DateOnly.ParseExact(fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
