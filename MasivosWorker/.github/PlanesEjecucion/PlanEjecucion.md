# Plan de Ejecución — MasivosWorker

> Basado en: [Requerimientos.md](../Requerimientos/Requerimientos.md)  
> Fecha: Mayo 2026  
> Estado del proyecto: En producción — mejoras incrementales

---

## Estado actual por Scope

Antes de listar las tareas, se registra qué está resuelto y qué no en cada scope definido en los requerimientos.

### Scope 1 — Detección y ciclo de vida de archivos

| Comportamiento esperado | Estado | Observación |
|---|---|---|
| Watcher detecta PDFs en `/procesar` | ✅ Implementado | `FileSystemWatcher` con filtro `*.pdf` |
| Máximo 2 archivos en paralelo | ✅ Implementado | `SemaphoreSlim(2)` |
| Deduplicación de eventos del watcher | ✅ Implementado | `HashSet<string>` con `lock` |
| Espera a que el archivo esté copiado | ✅ Implementado | 10 reintentos × 500 ms |
| Prefijo `CRC_900277244_` al mover a `/Procesando` | ✅ Implementado | `FileManagerInfraestructure.MoverAProcesando` |
| Prefijo `CRC_900277244_` al mover a `/Procesados` | ✅ Implementado | `FileManagerInfraestructure.MoverAProcesados` |
| Prefijo `CRC_900277244_` al mover a `/error` (ruta normal) | ✅ Implementado | El archivo ya viene de `/Procesando` con prefijo |
| **Prefijo `CRC_900277244_` al mover a `/error` (archivo nunca disponible)** | ❌ **GAP** | Cuando `EsperarArchivoDisponible` lanza excepción, `rutaProcesando` es `null` y el fallback envía la ruta original de `/procesar` sin prefijo a `/error` |
| Recuperación al reiniciar sin doble prefijo | ✅ Implementado (corregido) | `yaEnProcesando: true` omite `MoverAProcesando` |
| Carpetas creadas al iniciar | ✅ Implementado | `CrearCarpetasSiNoExisten()` |
| Accesos directos en escritorio | ✅ Implementado | `CrearAccesosDirectos()` |

### Scope 2 — Lectura de código de barras

| Comportamiento esperado | Estado | Observación |
|---|---|---|
| Lee solo la primera página del PDF | ✅ Implementado | `ToBitmap(400).ToList()[0]` |
| Páginas adicionales liberadas de memoria | ✅ Implementado (corregido) | Loop `Dispose()` para páginas 2..N |
| PDF original no modificado por lectura | ✅ Correcto | Solo se lee, no se escribe |
| 5 estrategias progresivas de lectura | ✅ Implementado | Región → región mejorada → completo → completo mejorado → cuadrantes |
| Hasta 3 reintentos completos por archivo | ✅ Implementado | `ProcesarConReintentos(maxIntentos: 3)` |
| Validación del formato del código (`^([A-Z]+)(\d+)$`) | ✅ Implementado | Regex en `ProcesarPdf` |
| Limpieza de espacios y guiones en el código leído | ✅ Implementado | `.Replace(" ", "").Replace("-", "")` |

### Scope 3 — Integración API Soportes

| Comportamiento esperado | Estado | Observación |
|---|---|---|
| POST con código de barras | ✅ Implementado | `SoporteApiService.EnviarSoporteAsync` |
| API Key leída de configuración | ✅ Implementado (corregido) | `appsettings.json → ApiCredentials:SoporteApiKey` |
| Archivo va a `/error` si la API falla | ✅ Implementado | Rama `respuesta == null` |
| Logging de respuesta con nombre del paciente | ✅ Implementado | `ApiSoporteOK | Paciente=...` |

### Scope 4 — Integración API Física

| Comportamiento esperado | Estado | Observación |
|---|---|---|
| POST multipart con todos los campos del DTO | ✅ Implementado | `SoporteFisicoApiService.EnviarSoporteFisicoAsync` |
| Bearer token leído de configuración | ✅ Implementado (corregido) | `appsettings.json → ApiCredentials:SoporteFisicoToken` |
| Se adjunta el PDF original completo (todas las páginas) | ✅ Correcto | `File.ReadAllBytesAsync(rutaArchivo)` — lee el archivo sin modificarlo |
| Archivo va a `/error` si la API falla | ✅ Implementado | Rama `!enviadoFisico` |
| **`idUsuario` hardcodeado como `"system"`** | ⚠️ **Pendiente definir** | Campo enviado con valor fijo — confirmar si es correcto o debe ser configurable |

### Scope 5 — Configuración y operación

| Comportamiento esperado | Estado | Observación |
|---|---|---|
| Credenciales en `appsettings.json` | ✅ Implementado (corregido) | `ApiCredentials`, `IronBarcode`, `Rutas`, `FileSettings` |
| Licencia IronBarcode inicializada una sola vez | ✅ Implementado (corregido) | Solo en `IronBarcodeLicenseInitializer` |
| Logging estructurado (`key=value`) | ✅ Implementado | Consistente en todas las clases |
| Estadísticas cada 10 archivos | ✅ Implementado | Total, OK, Error, PromedioMs, ErrorPct |
| Servicio registrado como Windows Service | ✅ Implementado | `AddWindowsService` |
| Inicialización falla → servicio se detiene | ✅ Implementado (corregido) | `return` en `ExecuteAsync` si lanza excepción |

---

## Tareas pendientes

### P1 — Crítico (afecta requerimientos documentados)

---

#### TAREA-01 — Garantizar prefijo en `/error` cuando el archivo nunca estuvo disponible

**Scope:** Detección y ciclo de vida de archivos  
**Problema:** Cuando `EsperarArchivoDisponible` lanza excepción después de 10 reintentos, `rutaProcesando` es `null`. El bloque `catch` de rescate llama `MoverAError(ruta)` donde `ruta` apunta al archivo original en `/procesar` — sin prefijo. El archivo llega a `/error` como `archivo_original.pdf`, violando el requerimiento de trazabilidad.

**Flujo afectado:**
```
PDF llega a /procesar
  → EsperarArchivoDisponible lanza excepción (archivo bloqueado 5 seg)
  → rutaProcesando == null
  → catch → MoverAError(ruta)   ← ruta original SIN prefijo
  → /error/archivo_original.pdf ← FALTA el prefijo
```

**Solución:** En el bloque `catch`, antes de llamar `MoverAError(ruta)`, aplicar el prefijo manualmente o agregar un método `MoverAErrorDesdeOrigen(string rutaOrigen)` en `FileManagerInfraestructure` que siempre aplica el prefijo independientemente de la carpeta de origen.

**Archivos a modificar:**
- [Infrastructure/FileManagerInfraestructure.cs](../../../Infrastructure/FileManagerInfraestructure.cs) — nuevo método `MoverAErrorDesdeOrigen`
- [Infrastructure/FileWatcherInfraestructure.cs](../../../Infrastructure/FileWatcherInfraestructure.cs) — usar el nuevo método en el `catch` de rescate

---

#### TAREA-02 — Confirmar valor de `idUsuario` en API Física

**Scope:** Integración API Física  
**Problema:** El campo `idUsuario` se envía como `"system"` hardcodeado. No está en `appsettings.json` ni documentado en los requerimientos.

**Preguntas a resolver antes de implementar:**
- ¿`"system"` es el valor correcto y permanente para este campo?
- ¿Debe ser configurable por sede/empresa?
- ¿La API de la intranet valida este campo o lo ignora?

**Archivos a modificar (si aplica):**
- [MasivosWorker/appsettings.json](../../../MasivosWorker/appsettings.json) — agregar `ApiCredentials:IdUsuario`
- [Services/SoporteFisicoApiService.cs](../../../Services/SoporteFisicoApiService.cs) — leerlo de configuración

---

### P2 — Importante (calidad y mantenibilidad)

---

#### TAREA-03 — Silenciar el `catch {}` vacío en el bloque de rescate

**Scope:** Detección y ciclo de vida de archivos  
**Problema:** Si `MoverAError` falla (disco lleno, permisos insuficientes, archivo ya movido por otro proceso), la excepción se traga sin dejar rastro en los logs. El archivo desaparece sin que el sistema sepa adónde fue.

```csharp
catch { }  // ← excepción de MoverAError desaparece aquí
```

**Solución:** Reemplazar `catch { }` por `catch (Exception moveEx) { _logger.LogError(moveEx, "ErrorMoverAError | Archivo={Archivo}", ...); }`.

**Archivos a modificar:**
- [Infrastructure/FileWatcherInfraestructure.cs](../../../Infrastructure/FileWatcherInfraestructure.cs)

---

#### TAREA-04 — Unificar versión de `BarCode` (IronBarcode) entre proyectos

**Scope:** Configuración y operación  
**Problema:** Dos versiones distintas conviven en la solución.

| Proyecto | Versión actual |
|---|---|
| `Services.csproj` | 2026.3.6 |
| `MasivosWorker.csproj` | 2026.4.2 |

NuGet resuelve el conflicto en tiempo de compilación, pero la diferencia puede producir comportamiento distinto entre la clase que lee barcodes (`BarcodeRegionService` en Services) y el proyecto principal.

**Solución:** Unificar ambos a la versión más reciente (2026.4.2).

**Archivos a modificar:**
- [Services/Services.csproj](../../../Services/Services.csproj)

---

#### TAREA-05 — Acotar `EsperarArchivoDisponible` con `CancellationToken`

**Scope:** Detección y ciclo de vida de archivos  
**Problema:** El bucle de espera no recibe el `CancellationToken` del servicio. Si el servicio se detiene mientras espera, el loop sigue corriendo hasta completar los 10 intentos (hasta 5 segundos de retraso en el apagado).

**Solución:** Propagar el token de cancelación como parámetro y pasarlo a `Task.Delay(500, cancellationToken)`.

**Archivos a modificar:**
- [Infrastructure/FileWatcherInfraestructure.cs](../../../Infrastructure/FileWatcherInfraestructure.cs)
- [Infrastructure/FileWatcherInfraestructure.cs](../../../Infrastructure/FileWatcherInfraestructure.cs) — `Iniciar()` debe recibir o capturar el token

---

### P3 — Mejoras futuras (no bloqueantes)

---

#### TAREA-06 — Agregar logging del número de páginas del PDF

**Scope:** Lectura de código de barras  
**Motivación:** Si un PDF de múltiples páginas falla en la lectura del código, no hay registro de cuántas páginas tenía. Útil para depuración.

**Cambio mínimo:** Loggear `pdf.PageCount` junto con el nombre del archivo al iniciar la lectura.

**Archivos a modificar:**
- [Services/BarcodeRegionService.cs](../../../Services/BarcodeRegionService.cs)

---

#### TAREA-07 — Hacer configurable el límite de concurrencia del semáforo

**Scope:** Detección y ciclo de vida de archivos  
**Motivación:** `SemaphoreSlim(2)` está hardcodeado. En equipos con más RAM se podría aumentar; en equipos con poca memoria, reducir a 1.

**Cambio:** Agregar `MaxConcurrentFiles: 2` en `appsettings.json → FileSettings` y leerlo en el constructor de `FileWatcherInfraestructure`.

**Archivos a modificar:**
- [Models/FileSettings.cs](../../../Models/FileSettings.cs)
- [MasivosWorker/appsettings.json](../../../MasivosWorker/appsettings.json)
- [Infrastructure/FileWatcherInfraestructure.cs](../../../Infrastructure/FileWatcherInfraestructure.cs)

---

#### TAREA-08 — Hacer configurable el número de reintentos de barcode

**Scope:** Lectura de código de barras  
**Motivación:** `maxIntentos = 3` y los `500ms` entre intentos están hardcodeados en `ProcesarConReintentos`. Ajustar sin recompilar sería útil en producción.

**Cambio:** Agregar `BarcodeMaxReintentos: 3` y `BarcodeEsperaMs: 500` en `appsettings.json → FileSettings`.

**Archivos a modificar:**
- [Models/FileSettings.cs](../../../Models/FileSettings.cs)
- [MasivosWorker/appsettings.json](../../../MasivosWorker/appsettings.json)
- [Infrastructure/FileWatcherInfraestructure.cs](../../../Infrastructure/FileWatcherInfraestructure.cs)

---

## Orden de ejecución recomendado

```
Sprint 1 (esta semana)
├── TAREA-01  ← Prefijo en /error al fallar disponibilidad    [P1 - bug]
├── TAREA-02  ← Confirmar idUsuario (requiere respuesta tuya) [P1 - definición]
└── TAREA-03  ← Loggear errores de MoverAError               [P2 - seguridad operativa]

Sprint 2 (siguiente semana)
├── TAREA-04  ← Unificar versión IronBarcode                  [P2 - consistencia]
└── TAREA-05  ← CancellationToken en EsperarArchivoDisponible [P2 - shutdown limpio]

Backlog (cuando haya capacidad)
├── TAREA-06  ← Log de páginas del PDF                        [P3]
├── TAREA-07  ← Concurrencia configurable                     [P3]
└── TAREA-08  ← Reintentos de barcode configurables           [P3]
```

---

## Resumen de archivos por tarea

| Tarea | Archivos modificados |
|---|---|
| TAREA-01 | `FileManagerInfraestructure.cs`, `FileWatcherInfraestructure.cs` |
| TAREA-02 | `appsettings.json`, `SoporteFisicoApiService.cs` *(si aplica)* |
| TAREA-03 | `FileWatcherInfraestructure.cs` |
| TAREA-04 | `Services.csproj` |
| TAREA-05 | `FileWatcherInfraestructure.cs` |
| TAREA-06 | `BarcodeRegionService.cs` |
| TAREA-07 | `FileSettings.cs`, `appsettings.json`, `FileWatcherInfraestructure.cs` |
| TAREA-08 | `FileSettings.cs`, `appsettings.json`, `FileWatcherInfraestructure.cs` |
