-- RadicaWeb API trazabilidad (MasivosWorker)
USE Scaneados;
GO

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
GO
