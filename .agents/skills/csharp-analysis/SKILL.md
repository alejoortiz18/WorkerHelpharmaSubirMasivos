---
name: csharp-analysis
description: "Analiza, explica y revisa código C# en este proyecto. Usa este skill para: entender clases existentes, detectar mejoras, revisar patrones de diseño, analizar async/await, DI, HttpClient, FileSystemWatcher, o cualquier análisis técnico de C# en MasivosWorker."
argument-hint: "Clase, método o fragmento de código C# a analizar"
---

# C# Code Analysis – MasivosWorker

## Cuándo Usar Este Skill

- Explicar qué hace una clase o método del proyecto
- Revisar si un patrón C# está bien implementado
- Detectar bugs o code smells en el código existente
- Comparar enfoques (ej. `Parallel.ForEach` vs `SemaphoreSlim`)
- Analizar uso de `async/await`, cancelación, manejo de excepciones
- Revisar configuración de `HttpClient`, `IOptions<T>`, `BackgroundService`
- Evaluar thread-safety en acceso concurrente a recursos compartidos

---

## Guías de Análisis por Área

### 1. BackgroundService y Hosted Services
- `Worker` hereda de `BackgroundService` y sobreescribe `ExecuteAsync(CancellationToken)`
- El loop infinito usa `await Task.Delay(Timeout.Infinite, stoppingToken)` — patrón correcto para mantener el servicio vivo
- La cancelación debe propagarse con `stoppingToken` a métodos internos

### 2. FileSystemWatcher (FileWatcherInfraestructure)
- El watcher dispara eventos en un thread pool thread — se requiere sincronización
- `SemaphoreSlim(2)` controla concurrencia máxima (2 archivos simultáneos)
- `HashSet<string>` con lock implícito previene procesamiento duplicado de eventos duplicados del watcher
- Patrón de espera de archivo disponible: reintentos con `File.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.None)`

### 3. HttpClient y APIs
- `HttpClient` registrado via `AddHttpClient<T>()` — correcto (evita socket exhaustion)
- `SoporteApiService`: POST con JSON body, header `X-API-KEY`
- `SoporteFisicoApiService`: POST multipart/form-data con `MultipartFormDataContent`, Bearer token
- Serialización: `System.Text.Json` con `JsonSerializerOptions { PropertyNameCaseInsensitive = true }`

### 4. IOptions<T> y Configuración Tipada
- `IOptions<RutasSettings>` — valor fijo al startup, acceder con `.Value`
- `IOptionsSnapshot<T>` — si se necesita reload en caliente (no usado aquí)
- `IOptionsMonitor<T>` — si se necesita callback en cambio (no usado aquí)

### 5. Async/Await Patrones
- `async void` debe evitarse — usar `async Task`
- Excepciones en `async void` no son capturables; verificar handlers del FileSystemWatcher
- `ConfigureAwait(false)` no es necesario en aplicaciones .NET (solo en librerías)
- `Task.WhenAll()` para paralelismo controlado cuando aplique

### 6. Manejo de Archivos
- Archivos grandes deben procesarse en streams, no `File.ReadAllBytes()` cuando sea posible
- `using` statement garantiza liberación de recursos (`FileStream`, `Bitmap`, etc.)
- Prefijo `KeyName` en nombre de archivo permite identificar estado "en proceso" fácilmente

### 7. IronBarcode / IronPDF
- Licencia inicializada como Singleton en startup — correcto, no re-inicializar por archivo
- PDFs convertidos a Bitmap a 400 DPI para mejor precisión de lectura
- Estrategias de región permiten focalizarse en zona del barcode antes de escanear todo el PDF
- `BarcodeReaderOptions.Multithreaded = true` con `ProcessorCount` threads

### 8. Thread Safety
- `HashSet<string> _archivosEnProcesamiento` necesita sincronización explícita (`lock`)
- Contadores `_procesadosOk`, `_procesadosError` — evaluar uso de `Interlocked.Increment`
- `SemaphoreSlim` es thread-safe para `WaitAsync()`/`Release()`

---

## Checklist de Revisión de Código C#

Cuando analices una clase, verifica:

- [ ] **Inyección de dependencias:** ¿Constructor injection? ¿`IOptions<T>` para config?
- [ ] **Async correctness:** ¿Awaits correctos? ¿Sin `async void`? ¿CancellationToken propagado?
- [ ] **Manejo de excepciones:** ¿`try/catch` específicos? ¿Logging de error completo?
- [ ] **Recursos liberados:** ¿`using` en `IDisposable`? ¿`FileStream`, `Bitmap` cerrados?
- [ ] **Thread safety:** ¿Colecciones compartidas protegidas? ¿Contadores atómicos?
- [ ] **HttpClient:** ¿Inyectado por DI? ¿No instanciado manualmente?
- [ ] **Logging:** ¿Niveles correctos? (`Debug` para detalle, `Information` para flujo, `Warning`/`Error` para problemas)
- [ ] **OWASP:** ¿Sin credenciales hardcodeadas en código? ¿API keys en configuración?

---

## Convenciones del Proyecto

```
Capa          | Sufijo de clase       | Ejemplo
--------------|-----------------------|---------------------------
Infrastructure| ...Infraestructura    | FileManagerInfraestructura
Services      | ...Service            | SoporteApiService
Models/DTOs   | ...Dto / ...Settings  | SoporteResponseDto
Worker        | Worker                | Worker
```

- Métodos en **español**
- Servicios registrados como **Singleton** en `Program.cs`
- Logging estructurado con `ILogger<T>`
- Configuración tipada con secciones en `appsettings.json`

---

## Procedimiento de Análisis

1. **Leer el archivo** — usar `read_file` con el path absoluto
2. **Identificar la responsabilidad** — ¿qué resuelve esta clase en el pipeline?
3. **Revisar dependencias** — inyecciones en constructor
4. **Analizar métodos** — flujo, async, manejo de errores
5. **Aplicar checklist** — thread safety, recursos, patrones
6. **Sugerir mejoras** — ordenadas por impacto (crítico → menor)
