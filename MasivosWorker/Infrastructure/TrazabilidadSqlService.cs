using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Models.Dto;

namespace Infrastructure;

public interface ITrazabilidadSqlService
{
    Task EnsureSchemaAsync(CancellationToken cancellationToken = default);

    Task RegistrarDocumentoAsync(
        RutasLoteContext contexto,
        string nombreArchivo,
        string? soporte,
        int? idPaciente,
        string? idBodega,
        string? idCartera,
        DateTime? fechaFactura,
        bool procesado,
        CancellationToken cancellationToken = default);

    Task RegistrarRadicaWebAsync(
        RutasLoteContext contexto,
        DateOnly fechaFactura,
        string bodega,
        RadicaWebBusquedaResultado resultado,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<(DateOnly Fecha, string Bodega)>> ObtenerCombinacionesRadicaWebAsync(
        RutasLoteContext contexto,
        CancellationToken cancellationToken = default);
}

public class TrazabilidadSqlService : ITrazabilidadSqlService
{
    private readonly string _connectionString;
    private readonly ILogger<TrazabilidadSqlService> _logger;

    public TrazabilidadSqlService(
        IOptions<TrazabilidadSqlSettings> settings,
        ILogger<TrazabilidadSqlService> logger)
    {
        _connectionString = settings.Value.ConnectionString;
        _logger = logger;
    }

    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        const string script = """
IF DB_ID(N'Scaneados') IS NULL
BEGIN
    CREATE DATABASE Scaneados;
END

USE Scaneados;

IF OBJECT_ID(N'dbo.DocumentosProcesados', N'U') IS NULL
BEGIN
    IF OBJECT_ID(N'dbo.FechasProcesamiento', N'U') IS NOT NULL DROP TABLE dbo.FechasProcesamiento;
    IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NOT NULL DROP TABLE dbo.Usuarios;

    CREATE TABLE dbo.Usuarios
    (
        UsuarioId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
        NombreUsuario nvarchar(100) NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Usuarios_FechaCreacion DEFAULT (sysdatetime()),
        CONSTRAINT UQ_Usuarios_NombreUsuario UNIQUE (NombreUsuario)
    );

    CREATE TABLE dbo.FechasProcesamiento
    (
        FechaProcesamientoId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FechasProcesamiento PRIMARY KEY,
        UsuarioId int NOT NULL,
        FechaProcesamiento date NOT NULL,
        FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_FechasProcesamiento_FechaCreacion DEFAULT (sysdatetime()),
        CONSTRAINT FK_FechasProcesamiento_Usuarios FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios(UsuarioId),
        CONSTRAINT UQ_FechasProcesamiento_Usuario_Fecha UNIQUE (UsuarioId, FechaProcesamiento)
    );

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

;WITH Duplicados AS
(
    SELECT
        DocumentoProcesadoId,
        FechaProcesamientoId,
        NombreArchivo,
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
),
ValoresConsolidados AS
(
    SELECT
        FechaProcesamientoId,
        NombreArchivo,
        MAX(CASE WHEN Soporte IS NOT NULL THEN Soporte END) AS Soporte,
        MAX(IdPaciente) AS IdPaciente,
        MAX(CASE WHEN Procesado = 1 THEN 1 ELSE 0 END) AS Procesado
    FROM dbo.DocumentosProcesados
    GROUP BY FechaProcesamientoId, NombreArchivo
)
UPDATE objetivo
SET
    objetivo.Soporte = COALESCE(valores.Soporte, objetivo.Soporte),
    objetivo.IdPaciente = COALESCE(valores.IdPaciente, objetivo.IdPaciente),
    objetivo.Procesado = valores.Procesado
FROM dbo.DocumentosProcesados objetivo
INNER JOIN Duplicados duplicado
    ON duplicado.DocumentoProcesadoId = objetivo.DocumentoProcesadoId
INNER JOIN ValoresConsolidados valores
    ON valores.FechaProcesamientoId = duplicado.FechaProcesamientoId
   AND valores.NombreArchivo = duplicado.NombreArchivo
WHERE duplicado.RowNum = 1;

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

IF OBJECT_ID(N'dbo.usp_RegistrarDocumentoProcesado', N'P') IS NULL
BEGIN
    EXEC(N'
    CREATE PROCEDURE dbo.usp_RegistrarDocumentoProcesado
        @NombreUsuario nvarchar(100),
        @FechaProcesamiento date,
        @NombreArchivo nvarchar(260),
        @Soporte nvarchar(100) = NULL,
        @IdPaciente int = NULL,
        @IdBodega nvarchar(100) = NULL,
        @IdCartera nvarchar(100) = NULL,
        @FechaFactura datetime2(0) = NULL,
        @Procesado bit
    AS
    BEGIN
        SET NOCOUNT ON;
        SET XACT_ABORT ON;

        BEGIN TRAN;

        DECLARE @UsuarioId int;
        DECLARE @FechaId int;

        SELECT @UsuarioId = UsuarioId
        FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK)
        WHERE NombreUsuario = @NombreUsuario;

        IF @UsuarioId IS NULL
        BEGIN
            INSERT INTO dbo.Usuarios (NombreUsuario)
            VALUES (@NombreUsuario);

            SET @UsuarioId = SCOPE_IDENTITY();
        END

        SELECT @FechaId = FechaProcesamientoId
        FROM dbo.FechasProcesamiento WITH (UPDLOCK, HOLDLOCK)
        WHERE UsuarioId = @UsuarioId
          AND FechaProcesamiento = @FechaProcesamiento;

        IF @FechaId IS NULL
        BEGIN
            INSERT INTO dbo.FechasProcesamiento (UsuarioId, FechaProcesamiento)
            VALUES (@UsuarioId, @FechaProcesamiento);

            SET @FechaId = SCOPE_IDENTITY();
        END

        UPDATE dbo.DocumentosProcesados
        SET
            Soporte = COALESCE(@Soporte, Soporte),
            IdPaciente = COALESCE(@IdPaciente, IdPaciente),
            IdBodega = COALESCE(@IdBodega, IdBodega),
            IdCartera = COALESCE(@IdCartera, IdCartera),
            FechaFactura = COALESCE(@FechaFactura, FechaFactura),
            Procesado = CASE WHEN @Procesado = 1 THEN 1 ELSE Procesado END
        WHERE FechaProcesamientoId = @FechaId
          AND NombreArchivo = @NombreArchivo;

        IF @@ROWCOUNT = 0
        BEGIN
            INSERT INTO dbo.DocumentosProcesados
            (
                FechaProcesamientoId,
                NombreArchivo,
                Soporte,
                IdPaciente,
                IdBodega,
                IdCartera,
                FechaFactura,
                Procesado
            )
            VALUES
            (
                @FechaId,
                @NombreArchivo,
                @Soporte,
                @IdPaciente,
                @IdBodega,
                @IdCartera,
                @FechaFactura,
                @Procesado
            );
        END

        COMMIT TRAN;
    END');
END

IF OBJECT_ID(N'dbo.RadicaWebAPI', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RadicaWebAPI
    (
        RadicaWebApiId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_RadicaWebAPI PRIMARY KEY,
        UsuarioId int NOT NULL,
        FechaFactura date NOT NULL,
        Bodega nvarchar(100) NOT NULL,
        Success bit NULL,
        Message nvarchar(max) NULL,
        SolicitudId int NULL,
        RegistrosInsertados int NULL,
        TotalRegistros int NULL,
        JobId nvarchar(200) NULL,
        StatusCode int NULL,
        Error nvarchar(200) NULL,
        Timestamp datetimeoffset(0) NULL,
        Path nvarchar(500) NULL,
        CreadoEn datetime2(0) NOT NULL CONSTRAINT DF_RadicaWebAPI_CreadoEn DEFAULT (sysdatetime()),
        CONSTRAINT FK_RadicaWebAPI_Usuarios FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios(UsuarioId)
    );

    CREATE INDEX IX_RadicaWebAPI_UsuarioId_CreadoEn
        ON dbo.RadicaWebAPI (UsuarioId, CreadoEn DESC);
END
""";

        await EjecutarSqlAsync(script, cancellationToken, databaseName: "master");
    }

    public async Task RegistrarDocumentoAsync(
        RutasLoteContext contexto,
        string nombreArchivo,
        string? soporte,
        int? idPaciente,
        string? idBodega,
        string? idCartera,
        DateTime? fechaFactura,
        bool procesado,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DECLARE @UsuarioId int;
DECLARE @FechaId int;

SELECT @UsuarioId = UsuarioId
FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK)
WHERE NombreUsuario = @NombreUsuario;

IF @UsuarioId IS NULL
BEGIN
    INSERT INTO dbo.Usuarios (NombreUsuario)
    VALUES (@NombreUsuario);

    SET @UsuarioId = SCOPE_IDENTITY();
END

SELECT @FechaId = FechaProcesamientoId
FROM dbo.FechasProcesamiento WITH (UPDLOCK, HOLDLOCK)
WHERE UsuarioId = @UsuarioId
  AND FechaProcesamiento = @FechaProcesamiento;

IF @FechaId IS NULL
BEGIN
    INSERT INTO dbo.FechasProcesamiento (UsuarioId, FechaProcesamiento)
    VALUES (@UsuarioId, @FechaProcesamiento);

    SET @FechaId = SCOPE_IDENTITY();
END

UPDATE dbo.DocumentosProcesados
SET
    Soporte = COALESCE(@Soporte, Soporte),
    IdPaciente = COALESCE(@IdPaciente, IdPaciente),
    IdBodega = COALESCE(@IdBodega, IdBodega),
    IdCartera = COALESCE(@IdCartera, IdCartera),
    FechaFactura = COALESCE(@FechaFactura, FechaFactura),
    Procesado = CASE WHEN @Procesado = 1 THEN 1 ELSE Procesado END
WHERE FechaProcesamientoId = @FechaId
  AND NombreArchivo = @NombreArchivo;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO dbo.DocumentosProcesados
    (
        FechaProcesamientoId,
        NombreArchivo,
        Soporte,
        IdPaciente,
        IdBodega,
        IdCartera,
        FechaFactura,
        Procesado
    )
    VALUES
    (
        @FechaId,
        @NombreArchivo,
        @Soporte,
        @IdPaciente,
        @IdBodega,
        @IdCartera,
        @FechaFactura,
        @Procesado
    );
END

COMMIT TRAN;
""";

        var fecha = DateOnly.ParseExact(contexto.Fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        try
        {
            await EjecutarSqlAsync(sql, cancellationToken, command =>
            {
                command.Parameters.AddWithValue("@NombreUsuario", contexto.Usuario);
                command.Parameters.Add("@FechaProcesamiento", System.Data.SqlDbType.Date).Value = fecha.ToDateTime(TimeOnly.MinValue);
                command.Parameters.AddWithValue("@NombreArchivo", nombreArchivo);
                command.Parameters.Add("@Soporte", System.Data.SqlDbType.NVarChar, 100).Value = (object?)soporte ?? DBNull.Value;
                command.Parameters.Add("@IdPaciente", System.Data.SqlDbType.Int).Value = (object?)idPaciente ?? DBNull.Value;
                command.Parameters.Add("@IdBodega", System.Data.SqlDbType.NVarChar, 100).Value = (object?)idBodega ?? DBNull.Value;
                command.Parameters.Add("@IdCartera", System.Data.SqlDbType.NVarChar, 100).Value = (object?)idCartera ?? DBNull.Value;
                command.Parameters.Add("@FechaFactura", System.Data.SqlDbType.DateTime2).Value = (object?)fechaFactura ?? DBNull.Value;
                command.Parameters.Add("@Procesado", System.Data.SqlDbType.Bit).Value = procesado;
            });

            _logger.LogInformation(
                "TrazabilidadDocumentoRegistrada | Usuario={Usuario} | Fecha={Fecha} | Archivo={Archivo} | Soporte={Soporte} | IdPaciente={IdPaciente} | IdBodega={IdBodega} | IdCartera={IdCartera} | FechaFactura={FechaFactura} | Procesado={Procesado}",
                contexto.Usuario,
                contexto.Fecha,
                nombreArchivo,
                soporte ?? "(null)",
                idPaciente?.ToString() ?? "(null)",
                idBodega ?? "(null)",
                idCartera ?? "(null)",
                fechaFactura?.ToString("yyyy-MM-dd HH:mm:ss") ?? "(null)",
                procesado);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(
                ex,
                "TrazabilidadSqlError | Usuario={Usuario} | Fecha={Fecha} | Archivo={Archivo} | El worker continuara con el proceso normal",
                contexto.Usuario,
                contexto.Fecha,
                nombreArchivo);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(
                ex,
                "TrazabilidadSqlError | Usuario={Usuario} | Fecha={Fecha} | Archivo={Archivo} | El worker continuara con el proceso normal",
                contexto.Usuario,
                contexto.Fecha,
                nombreArchivo);
        }
    }

    public async Task RegistrarRadicaWebAsync(
        RutasLoteContext contexto,
        DateOnly fechaFactura,
        string bodega,
        RadicaWebBusquedaResultado resultado,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DECLARE @UsuarioId int;

SELECT @UsuarioId = UsuarioId
FROM dbo.Usuarios WITH (UPDLOCK, HOLDLOCK)
WHERE NombreUsuario = @NombreUsuario;

IF @UsuarioId IS NULL
BEGIN
    INSERT INTO dbo.Usuarios (NombreUsuario)
    VALUES (@NombreUsuario);

    SET @UsuarioId = SCOPE_IDENTITY();
END

INSERT INTO dbo.RadicaWebAPI
(
    UsuarioId,
    FechaFactura,
    Bodega,
    Success,
    Message,
    SolicitudId,
    RegistrosInsertados,
    TotalRegistros,
    JobId,
    StatusCode,
    Error,
    Timestamp,
    Path
)
VALUES
(
    @UsuarioId,
    @FechaFactura,
    @Bodega,
    @Success,
    @Message,
    @SolicitudId,
    @RegistrosInsertados,
    @TotalRegistros,
    @JobId,
    @StatusCode,
    @Error,
    @Timestamp,
    @Path
);

COMMIT TRAN;
""";

        try
        {
            await EjecutarSqlAsync(sql, cancellationToken, command =>
            {
                command.Parameters.Add("@NombreUsuario", System.Data.SqlDbType.NVarChar, 100).Value = contexto.Usuario;
                command.Parameters.Add("@FechaFactura", System.Data.SqlDbType.Date).Value =
                    fechaFactura.ToDateTime(TimeOnly.MinValue);
                command.Parameters.Add("@Bodega", System.Data.SqlDbType.NVarChar, 100).Value = bodega;
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

            _logger.LogInformation(
                "RadicaWebTrazabilidadRegistrada | Usuario={Usuario} | FechaFactura={FechaFactura} | Bodega={Bodega} | Success={Success} | StatusCode={StatusCode}",
                contexto.Usuario,
                fechaFactura,
                bodega,
                resultado.Success?.ToString() ?? "(null)",
                resultado.HttpStatusCode?.ToString() ?? "(null)");
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(
                ex,
                "RadicaWebTrazabilidadSqlError | Usuario={Usuario} | FechaFactura={FechaFactura} | Bodega={Bodega}",
                contexto.Usuario,
                fechaFactura,
                bodega);
            throw;
        }
    }

    public async Task<IReadOnlyList<(DateOnly Fecha, string Bodega)>> ObtenerCombinacionesRadicaWebAsync(
        RutasLoteContext contexto,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
SELECT DISTINCT
    CAST(dp.FechaFactura AS date) AS FechaFactura,
    LTRIM(RTRIM(dp.IdBodega)) AS IdBodega
FROM dbo.DocumentosProcesados dp
INNER JOIN dbo.FechasProcesamiento fp ON fp.FechaProcesamientoId = dp.FechaProcesamientoId
INNER JOIN dbo.Usuarios u ON u.UsuarioId = fp.UsuarioId
WHERE u.NombreUsuario = @NombreUsuario
  AND fp.FechaProcesamiento = @FechaProcesamiento
  AND dp.Procesado = 1
  AND dp.FechaFactura IS NOT NULL
  AND dp.IdBodega IS NOT NULL
  AND LTRIM(RTRIM(dp.IdBodega)) <> N'';
""";

        var fechaProcesamiento = DateOnly.ParseExact(contexto.Fecha, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var combinaciones = new List<(DateOnly Fecha, string Bodega)>();
        var connectionString = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "Scaneados" }.ConnectionString;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Parameters.AddWithValue("@NombreUsuario", contexto.Usuario);
            command.Parameters.Add("@FechaProcesamiento", System.Data.SqlDbType.Date).Value =
                fechaProcesamiento.ToDateTime(TimeOnly.MinValue);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var fecha = DateOnly.FromDateTime(reader.GetDateTime(0));
                var bodega = reader.GetString(1);
                combinaciones.Add((fecha, bodega));
            }

            _logger.LogInformation(
                "RadicaWebCombinacionesObtenidas | Usuario={Usuario} | Fecha={Fecha} | Combinaciones={Combinaciones}",
                contexto.Usuario,
                contexto.Fecha,
                combinaciones.Count);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(
                ex,
                "RadicaWebCombinacionesSqlError | Usuario={Usuario} | Fecha={Fecha}",
                contexto.Usuario,
                contexto.Fecha);
        }

        return combinaciones;
    }

    public async Task EjecutarSqlAsync(
        string sql,
        CancellationToken cancellationToken = default,
        Action<SqlCommand>? configuracion = null,
        string? databaseName = null)
    {
        var connectionString = string.IsNullOrWhiteSpace(databaseName)
            ? _connectionString
            : new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = databaseName }.ConnectionString;

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = System.Data.CommandType.Text;
            configuracion?.Invoke(command);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            throw;
        }
    }
}

public class TrazabilidadSqlSettings
{
    public string ConnectionString { get; set; } = @"Server=ServiciosReleas\SQLEXPRESS;Database=Scaneados;Trusted_Connection=True;TrustServerCertificate=True;";
}
