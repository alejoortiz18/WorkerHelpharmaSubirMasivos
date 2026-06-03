# Instalación del servicio MoverDocumentos (Worker 1)

## Requisitos

- Windows 10/11 o Windows Server
- .NET 10 Runtime (o publicar como self-contained)
- Acceso de escritura a `\\192.168.0.69\ArchivosScaneados` (o ruta configurada en `appsettings.json`)
- Escáner configurado para guardar PDF en `C:\scaneo` (o `Rutas:CarpetaLocal`)

## Publicar

```powershell
cd MoverDocumentos\MoverDocumentos
dotnet publish -c Release -o C:\Servicios\MoverDocumentos
```

Copiar `appsettings.json` al directorio de publicación y ajustar `Rutas`, `Red` y `Lote` según el entorno.

## Registrar el servicio Windows

Ejecutar PowerShell **como administrador**:

```powershell
$exe = "C:\Servicios\MoverDocumentos\MoverDocumentos.exe"

sc.exe create MoverDocumentos binPath= "`"$exe`"" start= auto DisplayName= "Helpharma Mover Documentos (Worker 1)"
sc.exe description MoverDocumentos "Mueve PDFs de C:\scaneo a la unidad compartida y genera lotes TXT para MasivosWorker."

# Iniciar
sc.exe start MoverDocumentos
```

### Cuenta del servicio

Por defecto el servicio corre como `Local System`. Si la UNC requiere credenciales de dominio:

1. Abrir `services.msc` → **MoverDocumentos** → Propiedades → pestaña **Iniciar sesión**.
2. Usar una cuenta con permisos en la carpeta compartida, **o**
3. Configurar en `appsettings.json`:

```json
"Red": {
  "Usuario": "DOMINIO\\escaneados",
  "Clave": "<desde variable de entorno o secreto>",
  "UsarCredencialesConfiguradas": true
}
```

## Desinstalar

```powershell
sc.exe stop MoverDocumentos
sc.exe delete MoverDocumentos
```

## Verificar logs

- **Desarrollo:** consola al ejecutar `dotnet run --environment Development`
- **Producción:** Visor de eventos → Registros de Windows → **Aplicación** → origen **MoverDocumentos**

Mensajes esperados: `MoverDocumentosIniciado`, `CarpetaLocalLista`, `UsuarioDetectado`, `ArchivoMovido`, `LoteCerrado`, `RedNoDisponible`.

## Ejecución manual (producción)

```powershell
cd MoverDocumentos\MoverDocumentos
dotnet run
```

Rutas por defecto: `C:\scaneo` → `\\192.168.0.69\ArchivosScaneados`, TXT en `ArchivosNuevos`.
