---
name: masivos-worker-context
description: "Contexto completo del proyecto MasivosWorker. Usa este skill cuando necesites entender la arquitectura, clases, flujo de procesamiento de PDFs, DTOs, APIs, configuración o cualquier aspecto del Worker de soportes masivos de Helpharma."
argument-hint: "Pregunta sobre el proyecto, clase o flujo específico"
---

# MasivosWorker – Contexto Completo del Proyecto

## Descripción General

**MasivosWorker** es un Windows Service en **.NET 10.0** que automatiza el procesamiento masivo de soportes físicos de Helpharma. Monitorea una carpeta de entrada, extrae códigos de barras de PDFs con IronBarcode, consulta datos del soporte en la API de Helpharma y sube el documento adjunto con todos sus datos a la intranet.

---

## Arquitectura de Proyectos (Solución: `MasivosWorker.slnx`)

| Proyecto | Tipo | Responsabilidad |
|---|---|---|
| `MasivosWorker` | Worker Service (.NET 10) | Entry point, DI, servicio principal |
| `Business` | Class Library | Lógica de negocio (actualmente vacío, preparado para crecer) |
| `Services` | Class Library | Integraciones con APIs externas |
| `Infrastructure` | Class Library | FileWatcher, FileManager, licencias |
| `Models` | Class Library | DTOs y configuraciones |

---

## Flujo de Procesamiento de Archivos

```
[Carpeta Procesar]
      │
      ▼ (FileSystemWatcher detecta nuevo PDF)
[SemaphoreSlim(2) — máx 2 archivos simultáneos]
      │
      ▼ MoverAProcesando()
[Carpeta Procesando]  ←  prefijo: CRC_900277244_
      │
      ▼ BarcodeRegionService.ProcesarPdf()
[Extracción de código de barras]  (hasta 3 reintentos, 5 estrategias)
      │
      ├─── No encontrado ──→ [Carpeta Error]
      │
      ▼ SoporteApiService.EnviarSoporteAsync()
[POST → api-soportes.helpharma.com.co]  devuelve SoporteResponseDto
      │
      ├─── Error HTTP ──→ [Carpeta Error]
      │
      ▼ SoporteFisicoApiService.EnviarSoporteFisicoAsync()
[POST multipart → intranet.helpharma.com/api/v1/soporte/fisico]
      │
      ├─── Fallo ──→ [Carpeta Error]
      │
      ▼ MoverAProcesados()
[Carpeta Procesados]  (archivo procesado exitosamente)
```

---

## Clases Principales

### `Worker` (MasivosWorker/Worker.cs)
- Hereda: `BackgroundService`
- Inyecta: `FileManagerInfraestructure`, `FileWatcherInfraestructure`
- `ExecuteAsync()`:
  1. Crea carpetas si no existen
  2. Crea accesos directos en el escritorio
  3. Procesa archivos pendientes en "Procesando" (recuperación al reiniciar)
  4. Inicia el watcher
  5. Loop infinito (`Task.Delay(Timeout.Infinite)`)

### `FileWatcherInfraestructure` (Infrastructure/FileWatcherInfraestructure.cs)
- **Control de concurrencia:** `SemaphoreSlim(2)`
- **Estado de archivos:** `HashSet<string>` para evitar duplicados
- **Estadísticas:** contadores `_procesadosOk`, `_procesadosError`, tiempo promedio, tasa de error (log cada 10 archivos)
- **Métodos clave:**
  - `Iniciar()` — configura `FileSystemWatcher` con filtro `*.pdf`
  - `ProcesarPendientesAlIniciar()` — recupera archivos del estado "Procesando"
  - `ProcesarArchivoAsync(string)` — orquesta todo el pipeline
  - `ProcesarConReintentos()` — hasta 3 intentos de lectura de barcode
  - `EsperarArchivoDisponible()` — 10 reintentos, 500ms entre cada uno

### `FileManagerInfraestructure` (Infrastructure/FileManagerInfraestructure.cs)
- **Configuración:** `RutasSettings`, `FileSettings` (prefijo `KeyName`)
- **Métodos:**
  - `CrearCarpetasSiNoExisten()` — Procesar, Procesando, Error, Procesados
  - `CrearAccesosDirectos()` — atajos en el escritorio vía `IWshRuntimeLibrary`
  - `MoverAProcesando(string)` — agrega prefijo `KeyName` al nombre
  - `MoverAProcesados(string, string)` — mueve a carpeta final
  - `MoverAError(string)` — mueve a carpeta de errores

### `BarcodeRegionService` (Services/BarcodeRegionService.cs)
- **Método principal:** `ProcesarPdf(string)` → `DocumentoProcesadoDto?`
- **Extracción:** `LeerCodigoDesdePdf(string)` — 5 estrategias progresivas:
  1. Región superior derecha (55-100% ancho, 0-30% alto)
  2. Misma región con imagen mejorada
  3. PDF completo
  4. PDF completo con mejora
  5. Bloques por cuadrantes (último recurso)
- **Configuración IronBarcode:** 400 DPI, Speed.Balanced, AutoRotate=true, ConfidenceThreshold=0.5, multihilo
- **Regex barcode:** `^([A-Z]+)(\d+)$` — ej. `SOPORTE12345` → Prefijo=`SOPORTE`, Numero=`12345`
- **Mejora de imagen:** `ColorMatrix` con brillo 1.4x, ajuste saturación -0.2

### `SoporteApiService` (Services/SoporteApiService.cs)
- **Endpoint:** `POST https://api-soportes.helpharma.com.co/api/DocSoporte/soportes/DatosSoportes`
- **Auth:** Header `X-API-KEY: ABC123456789`
- **Body:** `{ "soporte": "<codigo>" }`
- **Retorna:** `SoporteResponseDto?` (deserialización JSON case-insensitive)

### `SoporteFisicoApiService` (Services/SoporteFisicoApiService.cs)
- **Endpoint:** `POST https://intranet.helpharma.com/api/v1/soporte/fisico`
- **Auth:** `Bearer 4050281|BTH7oV8sR3n5pc4Ko8LHxpnhbWiJKga8p6M3IAjw`
- **Envío:** `multipart/form-data` con todos los campos del `SoporteResponseDto` + adjunto PDF
- **Retorna:** `bool` (éxito/fallo)

### `IronBarcodeLicenseInitializer` (Infrastructure/IronBarcodeLicenseInitializer.cs)
- Singleton que asigna `License.LicenseKey` al iniciar desde `IronBarcodeSettings`

---

## DTOs (Models/Dto/)

| Clase | Propiedades principales |
|---|---|
| `CodigoBarrasDto` | `Prefijo`, `Numero` |
| `DocumentoProcesadoDto` | `Prefijo`, `Numero`, `NombreArchivo`, `Archivo (byte[])` |
| `IronBarcodeSettings` | `LicenseKey` |
| `MedicamentoDto` | `ordenes`, `producto`, `nombre`, `cantidad` |
| `RutasSettings` | `Procesar`, `Procesando`, `Error`, `Procesados` |
| `SoporteResponseDto` | `IdConvenio`, `NombreConvenio`, `Fecha`, `IdBodega`, + datos paciente, medicamentos, etc. |

### `FileSettings` (Models/FileSettings.cs)
```csharp
public string KeyName { get; set; }  // Prefijo para archivos en proceso, ej: "CRC_900277244_"
```

---

## Configuración (`appsettings.json`)

```json
{
  "IronBarcode": {
    "LicenseKey": "<clave>"
  },
  "Rutas": {
    "Procesar":    "...",
    "Procesando":  "...",
    "Error":       "...",
    "Procesados":  "..."
  },
  "FileSettings": {
    "KeyName": "CRC_900277244_"
  }
}
```

---

## Dependencias NuGet Clave

| Paquete | Versión | Uso |
|---|---|---|
| `BarCode` (IronBarcode) | 2026.x | Lectura de códigos de barras en PDFs |
| `IronPdf` | 2026.3.1 | Conversión PDF → Bitmap |
| `Microsoft.Extensions.Hosting.WindowsServices` | 10.0.5 | Servicio de Windows |
| `Microsoft.Extensions.Http` | 10.0.5 | HttpClientFactory |
| `Interop.IWshRuntimeLibrary` | 1.0.1 | Accesos directos en escritorio |
| `lbEscaneaDocs.dll` | Local | Librería custom de escaneo |

---

## Convenciones del Proyecto

- **Nomenclatura:** Clases con sufijo de su capa (`Infrastructure`, `Service`, `Dto`)
- **Inyección:** Constructor injection con `IOptions<T>` para configuración tipada
- **Logging:** `ILogger<T>` en todas las clases
- **Async/Await:** Todo el pipeline es async
- **Español:** Nombres de métodos y variables en español
- **Singleton:** Todos los servicios registrados como `AddSingleton`
