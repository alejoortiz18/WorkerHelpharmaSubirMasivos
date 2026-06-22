IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'ServiciosReleas\serviciosrelease')
BEGIN
    CREATE LOGIN [ServiciosReleas\serviciosrelease] FROM WINDOWS;
END
GO

IF DB_ID(N'Scaneados') IS NULL
BEGIN
    CREATE DATABASE Scaneados;
END
GO

USE Scaneados;
GO

IF OBJECT_ID(N'dbo.DocumentosProcesados', N'U') IS NOT NULL DROP TABLE dbo.DocumentosProcesados;
IF OBJECT_ID(N'dbo.FechasProcesamiento', N'U') IS NOT NULL DROP TABLE dbo.FechasProcesamiento;
IF OBJECT_ID(N'dbo.Usuarios', N'U') IS NOT NULL DROP TABLE dbo.Usuarios;
GO

CREATE TABLE dbo.Usuarios
(
    UsuarioId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Usuarios PRIMARY KEY,
    NombreUsuario nvarchar(100) NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_Usuarios_FechaCreacion DEFAULT (sysdatetime()),
    CONSTRAINT UQ_Usuarios_NombreUsuario UNIQUE (NombreUsuario)
);
GO

CREATE TABLE dbo.FechasProcesamiento
(
    FechaProcesamientoId int IDENTITY(1,1) NOT NULL CONSTRAINT PK_FechasProcesamiento PRIMARY KEY,
    UsuarioId int NOT NULL,
    FechaProcesamiento date NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_FechasProcesamiento_FechaCreacion DEFAULT (sysdatetime()),
    CONSTRAINT FK_FechasProcesamiento_Usuarios FOREIGN KEY (UsuarioId) REFERENCES dbo.Usuarios(UsuarioId),
    CONSTRAINT UQ_FechasProcesamiento_Usuario_Fecha UNIQUE (UsuarioId, FechaProcesamiento)
);
GO

CREATE TABLE dbo.DocumentosProcesados
(
    DocumentoProcesadoId bigint IDENTITY(1,1) NOT NULL CONSTRAINT PK_DocumentosProcesados PRIMARY KEY,
    FechaProcesamientoId int NOT NULL,
    NombreArchivo nvarchar(260) NOT NULL,
    Soporte nvarchar(100) NULL,
    IdPaciente int NULL,
    Procesado bit NOT NULL,
    FechaCreacion datetime2(0) NOT NULL CONSTRAINT DF_DocumentosProcesados_FechaCreacion DEFAULT (sysdatetime()),
    CONSTRAINT FK_DocumentosProcesados_FechasProcesamiento FOREIGN KEY (FechaProcesamientoId) REFERENCES dbo.FechasProcesamiento(FechaProcesamientoId)
);
GO

CREATE INDEX IX_DocumentosProcesados_FechaProcesamientoId
    ON dbo.DocumentosProcesados (FechaProcesamientoId);
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'ServiciosReleas\serviciosrelease')
BEGIN
    CREATE USER [ServiciosReleas\serviciosrelease] FOR LOGIN [ServiciosReleas\serviciosrelease];
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    INNER JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    INNER JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = N'db_datareader'
      AND m.name = N'ServiciosReleas\serviciosrelease'
)
BEGIN
    ALTER ROLE db_datareader ADD MEMBER [ServiciosReleas\serviciosrelease];
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members rm
    INNER JOIN sys.database_principals r ON rm.role_principal_id = r.principal_id
    INNER JOIN sys.database_principals m ON rm.member_principal_id = m.principal_id
    WHERE r.name = N'db_datawriter'
      AND m.name = N'ServiciosReleas\serviciosrelease'
)
BEGIN
    ALTER ROLE db_datawriter ADD MEMBER [ServiciosReleas\serviciosrelease];
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_RegistrarDocumentoProcesado
    @NombreUsuario nvarchar(100),
    @FechaProcesamiento date,
    @NombreArchivo nvarchar(260),
    @Soporte nvarchar(100) = NULL,
    @IdPaciente int = NULL,
    @Procesado bit
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UsuarioId int;
    DECLARE @FechaId int;

    SELECT @UsuarioId = UsuarioId
    FROM dbo.Usuarios
    WHERE NombreUsuario = @NombreUsuario;

    IF @UsuarioId IS NULL
    BEGIN
        INSERT INTO dbo.Usuarios (NombreUsuario)
        VALUES (@NombreUsuario);

        SET @UsuarioId = SCOPE_IDENTITY();
    END

    SELECT @FechaId = FechaProcesamientoId
    FROM dbo.FechasProcesamiento
    WHERE UsuarioId = @UsuarioId
      AND FechaProcesamiento = @FechaProcesamiento;

    IF @FechaId IS NULL
    BEGIN
        INSERT INTO dbo.FechasProcesamiento (UsuarioId, FechaProcesamiento)
        VALUES (@UsuarioId, @FechaProcesamiento);

        SET @FechaId = SCOPE_IDENTITY();
    END

    INSERT INTO dbo.DocumentosProcesados
    (
        FechaProcesamientoId,
        NombreArchivo,
        Soporte,
        IdPaciente,
        Procesado
    )
    VALUES
    (
        @FechaId,
        @NombreArchivo,
        @Soporte,
        @IdPaciente,
        @Procesado
    );
END
GO
