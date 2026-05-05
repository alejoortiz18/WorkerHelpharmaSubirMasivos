# Plan de Pruebas — MasivosWorker

## Información General

| Campo | Valor |
|---|---|
| Proyecto | WorkerHelpharmaSubirMasivos |
| Fecha | 2026-05-05 |
| Responsable | |
| Versión | 1.0 |

---

## 1. Objetivos

- Validar el correcto procesamiento de archivos PDF con códigos de barras.
- Garantizar que los servicios de API responden y manejan errores adecuadamente.
- Verificar la integración entre los módulos `Services`, `Business` e `Infrastructure`.

---

## 2. Alcance

### 2.1 En alcance
- `BarcodeRegionService` — lectura y parsing de códigos desde PDF.
- `SoporteApiService` — envío de soportes digitales a la API.
- `SoporteFisicoApiService` — envío de soportes físicos.
- `FileManagerInfraestructure` — gestión de archivos (mover, copiar, eliminar).
- `FileWatcherInfraestructure` — detección de nuevos archivos en la ruta vigilada.
- `Worker` — ciclo principal del servicio en background.

### 2.2 Fuera de alcance
- Pruebas de rendimiento / carga.
- Pruebas de seguridad de red.

---

## 3. Tipos de Prueba

| Tipo | Herramienta | Carpeta |
|---|---|---|
| Unitarias | xUnit + FluentAssertions + NSubstitute | `Tests/` |
| Integración | xUnit + servicios reales con configuración local | `Tests/Integration/` |
| Manuales / exploración | — | esta carpeta |

---

## 4. Casos de Prueba Unitarias

### 4.1 BarcodeRegionService

| ID | Caso | Entrada | Resultado esperado |
|---|---|---|---|
| BRS-01 | Archivo no existe | Ruta inexistente | `null` |
| BRS-02 | Ruta nula | `null` | `null` |
| BRS-03 | Ruta vacía | `""` | `null` |
| BRS-04 | PDF con código válido | PDF con barcode `ABC12345` | `DocumentoProcesadoDto { Prefijo="ABC", Numero="12345" }` |
| BRS-05 | PDF sin código | PDF sin barcode | `null` |
| BRS-06 | Código con espacios/guiones | `"ABC 123-45"` → normaliza → `"ABC12345"` | DTO correcto |
| BRS-07 | Código con formato inválido | `"12345ABC"` (número antes de letras) | `null` |

### 4.2 FileManagerInfraestructure

| ID | Caso | Resultado esperado |
|---|---|---|
| FM-01 | Mover archivo existente a destino | Archivo en nueva ruta, eliminado en origen |
| FM-02 | Mover archivo inexistente | No lanza excepción, loguea advertencia |
| FM-03 | Directorio destino no existe | Se crea el directorio automáticamente |

### 4.3 Worker (ExecuteAsync)

| ID | Caso | Resultado esperado |
|---|---|---|
| WK-01 | CancellationToken cancelado al inicio | Worker se detiene limpiamente |
| WK-02 | Servicio procesa archivos | Se invoca al menos una vez el servicio de procesamiento |

---

## 5. Casos de Prueba de Integración

| ID | Caso | Precondición | Resultado esperado |
|---|---|---|---|
| INT-01 | Subir soporte digital a API | API disponible en entorno de pruebas | HTTP 200 y ID de soporte en respuesta |
| INT-02 | Subir soporte físico a API | API disponible en entorno de pruebas | HTTP 200 |
| INT-03 | API devuelve 400 | Cuerpo de request inválido | Se loguea error, no se lanza excepción no controlada |
| INT-04 | API no disponible (timeout) | API apagada | Se loguea error de conexión, el worker continúa |

---

## 6. Criterios de Aceptación

- [ ] Todos los tests unitarios pasan (`dotnet test`).
- [ ] Cobertura de código ≥ 70 % en `Services/`.
- [ ] Ningún test tiene dependencia de red o sistema de archivos real.
- [ ] Los tests corren en menos de 10 segundos en total.

---

## 7. Ambiente y Configuración

```jsonc
// appsettings.Development.json (valores para pruebas locales)
{
  "Rutas": {
    "Entrada": "C:/temp/masivos/entrada",
    "Procesados": "C:/temp/masivos/procesados",
    "Errores": "C:/temp/masivos/errores"
  },
  "ApiCredentials": {
    "BaseUrl": "https://api-dev.helpharma.com"
  }
}
```

---

## 8. Comandos Útiles

```bash
# Ejecutar todos los tests
dotnet test MasivosWorker/Tests/Tests.csproj

# Ejecutar con cobertura
dotnet test MasivosWorker/Tests/Tests.csproj --collect:"XPlat Code Coverage"

# Ejecutar solo un test específico
dotnet test --filter "FullyQualifiedName~BarcodeRegionServiceTests"
```

---

## 9. Historial de Ejecución

| Fecha | Versión | Tests OK | Tests Fallidos | Notas |
|---|---|---|---|---|
| | | | | |
