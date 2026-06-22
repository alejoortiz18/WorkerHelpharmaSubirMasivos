# Pitch — MasivosWorker: Radicación Automática de Soportes Físicos

> Metodología: [Shape Up — Basecamp](https://basecamp.com/shapeup)  
> Fecha: Mayo 2026  
> Autor: Helpharma — Equipo de Desarrollo

---

## Problema

Los domiciliarios de Helpharma entregan medicamentos a pacientes y reciben una firma en un documento físico (soporte). Ese soporte se escanea como PDF y queda en una carpeta compartida. El proceso de radicarlo manualmente en el sistema — identificar el soporte, buscar los datos del paciente, adjuntar el archivo — consume tiempo operativo y genera errores de omisión: soportes que nunca se radican, archivos perdidos o mal nombrados.

El problema no es la tecnología, es la fricción: un operador debe hacer una acción manual por cada PDF que llega. Con volúmenes masivos (cientos de soportes por día), eso no escala.

---

## Appetite

**Ciclo corto — 2 semanas.**

El alcance está acotado: un proceso automático, una carpeta de entrada, dos APIs conocidas. No se construye UI, no se gestiona base de datos propia, no hay autenticación de usuarios. El riesgo principal es la lectura del código de barras, que ya tiene solución implementada.

---

## Solución

### Flujo general

```
[Operador deposita PDF en /procesar]
        │
        ▼
[Worker detecta el archivo]
        │
        ├─ Espera que el archivo esté completamente copiado
        ├─ Lo mueve a /Procesando  (con prefijo CRC_900277244_)
        │
        ▼
[Extrae código de barras del PDF]
        │
        ├─ No encontrado → /error  (prefijo CRC_900277244_ conservado)
        │
        ▼
[Consulta API de soportes → obtiene datos del paciente]
        │
        ├─ Error API → /error  (prefijo CRC_900277244_ conservado)
        │
        ▼
[Sube soporte físico a intranet con datos + PDF adjunto]
        │
        ├─ Error → /error  (prefijo CRC_900277244_ conservado)
        │
        ▼
[Mueve a /Procesados  con prefijo CRC_900277244_ + código leído]
```

### Carpetas del sistema

| Carpeta | Rol |
|---|---|
| `C:\Masivos\procesar` | Bandeja de entrada — el operador solo interactúa aquí |
| `C:\Masivos\Procesando` | En vuelo — archivo siendo procesado en este momento |
| `C:\Masivos\Procesados` | Radicado con éxito |
| `C:\Masivos\error` | Falló en algún paso — requiere revisión manual |

### Identificación de archivos — Prefijo

**Todo archivo que entre al sistema lleva el prefijo `CRC_900277244_` desde el momento en que pasa de `/procesar` a cualquier otra carpeta, sin excepción.**

- Al mover a `/Procesando`: `CRC_900277244_archivo_original.pdf`
- Al mover a `/Procesados`: `CRC_900277244_SOPORTE12345.pdf`
- Al mover a `/error`: `CRC_900277244_archivo_original.pdf`

El prefijo identifica la sede/empresa y nunca se elimina. Es la garantía de trazabilidad: cualquier archivo sin prefijo en las carpetas de salida indica un error en el pipeline.

### Integridad del archivo PDF

**El archivo PDF que se mueve entre carpetas siempre es el original completo tal como llegó — con todas sus páginas.**

El worker solo lee el PDF para extraer el código de barras de la primera página. No lo modifica, no lo recorta, no lo convierte a otro formato. El archivo que llega al endpoint de la intranet (`/api/v1/soporte/fisico`) es byte-a-byte idéntico al archivo que el operador depositó en `/procesar`.

Esto aplica en todos los casos:
- **Procesado exitoso**: el PDF original (todas las páginas) llega como adjunto a la API.
- **Error**: el PDF original (todas las páginas) se mueve a `/error` para revisión.
- **Pendiente al reiniciar**: el PDF original permanece en `/Procesando` y se retoma íntegro.

### Lectura del código de barras

Se asume que el código está en la **primera página** del PDF, preferentemente en la esquina superior derecha. El sistema aplica 5 estrategias progresivas de lectura antes de declarar fallo, con hasta 3 reintentos por archivo.

### Recuperación ante caídas

Si el servicio se interrumpe con archivos en `/Procesando`, al reiniciar los detecta y los retoma desde ese punto — sin volver a moverlos ni duplicar el prefijo.

---

## Rabbit Holes

**1. PDFs con múltiples páginas**  
El sistema lee solo la primera página para el código de barras, pero mueve el archivo completo en todas las operaciones de archivo. No hay lógica que divida, recorte o procese páginas adicionales — eso está fuera del alcance y sería una trampa de complejidad.

**2. Doble prefijo al recuperar archivos**  
Resuelto: la función de recuperación al reinicio (`ProcesarPendientesAlIniciar`) distingue si el archivo ya está en `/Procesando` y omite el paso de `MoverAProcesando`. El prefijo nunca se aplica dos veces.

**3. Archivos que aún se están copiando**  
El watcher dispara el evento en cuanto el archivo aparece en el sistema de archivos, no cuando termina de copiarse. El sistema espera hasta 5 segundos (10 reintentos × 500 ms) por acceso exclusivo antes de procesar.

**4. Concurrencia**  
Máximo 2 archivos procesados en simultáneo (`SemaphoreSlim(2)`). Aumentar este número sin medir el consumo de memoria de IronBarcode (que carga bitmaps de 400 DPI) puede causar presión de memoria.

**5. Credenciales de APIs**  
El API Key y el Bearer token viven en `appsettings.json`, no en el código fuente. Cambiarlos no requiere recompilar.

---

## No-Gos

- **No se construye interfaz de usuario.** El operador interactúa solo con carpetas del sistema de archivos.
- **No se implementa base de datos propia.** El estado del procesamiento se infiere de la carpeta donde está el archivo.
- **No se procesan páginas adicionales del PDF.** Solo la primera página se usa para extraer el código de barras.
- **No se modifica el contenido del PDF en ningún momento.** El archivo adjunto a la API es siempre el original.
- **No se elimina el prefijo `CRC_900277244_` bajo ninguna circunstancia.** Ni en éxito, ni en error, ni en recuperación.
- **No se procesan formatos distintos a PDF.** El watcher filtra exclusivamente `*.pdf`.
- **No se notifica al operador en tiempo real.** El feedback es la carpeta donde termina el archivo (`/Procesados` o `/error`).

---

## Scopes de implementación

Para referencia de construcción, el trabajo se divide en estos scopes:

| Scope | Qué incluye |
|---|---|
| **Detección y ciclo de vida de archivos** | FileWatcher, FileManager, carpetas, prefijo, recuperación al reinicio |
| **Lectura de código de barras** | BarcodeRegionService, 5 estrategias, reintentos, validación regex |
| **Integración API Soportes** | SoporteApiService, manejo de respuesta, errores |
| **Integración API Física** | SoporteFisicoApiService, multipart, adjunto PDF original completo |
| **Configuración y operación** | appsettings, accesos directos escritorio, logging, estadísticas |
