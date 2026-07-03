# Requerimiento: Integración del Worker Masivo con API de RadicaWeb

## Objetivo

Modificar el comportamiento del **Programa Masivo Worker** para que, una vez finalizado el procesamiento completo de un **lote (TXT)**, invoque el API de RadicaWeb y almacene la trazabilidad de cada solicitud realizada.

> **Alcance inicial:** MasivosWorker únicamente. La visualización en el portal **GestionArchivosEscaneados** se abordará en una fase posterior.

---

## Contexto: ¿Qué es RadicaWeb?

**RadicaWeb** es la plataforma/API de radicación de Helpharma (`api-radicacion.helpharma.com.co`). A diferencia del flujo documento a documento que ya realiza el worker (barcode → DatosSoportes → soporte/fisico), RadicaWeb recibe una **fecha** y una **bodega** y **encola una búsqueda masiva** de soportes físicos en segundo plano.

El worker no sustituye ese proceso; lo **dispara al cierre del lote** con las combinaciones únicas obtenidas de los PDFs procesados exitosamente.

---

# 1. Comportamiento del Worker

## 1.1 Monitoreo de lotes

El worker monitorea la ruta:

```text
\\192.168.0.69\ArchivosScaneados\ArchivosNuevos
```

En esta carpeta no llegan PDFs directamente, sino archivos **`.txt`** (lotes). Cada TXT apunta a una carpeta `...\usuario\fecha\procesar` con uno o más PDFs.

## 1.2 Orden del procesamiento (cuándo llamar RadicaWeb)

RadicaWeb **no** se invoca por PDF ni durante los reintentos intermedios. El orden obligatorio es:

1. **Intento 1** — Lectura por barcode (carpeta `procesar`, por tandas).
2. **Intento 2** — Reintento (carpeta `error`).
3. **Intento 3** — OpenAI (carpeta `procesaria`). **Último paso de lectura del PDF.**
4. Correo de fallo OpenAI (si aplica). No bloquea RadicaWeb.
5. **RadicaWeb** — Encolar búsquedas por combinaciones únicas `(fecha, bodega)` acumuladas en memoria.
6. Limpiar archivos temporales (comportamiento actual).
7. Eliminar el TXT del lote (comportamiento actual).
8. Cierre del lote (`Completado`).

> **Importante:** La llamada a RadicaWeb ocurre **después de los tres intentos** (incluido OpenAI) y **antes de eliminar el TXT**.

### Excepción: lote con incidencia de infraestructura

Si el lote queda en **`PendienteReintento`** por incidencia de infraestructura (red, movimiento de archivos, etc.):

- **Sí** se llama a RadicaWeb (tras los 3 intentos, con combinaciones de SQL o memoria).
- **No** se elimina el TXT.
- **No** se limpian temporales.
- El lote se reintenta en un ciclo posterior.

---

## 1.3 Endpoint RadicaWeb

```http
POST https://api-radicacion.helpharma.com.co/api/physical-supports/integrations/busqueda
```

### Headers

| Header | Descripción |
|--------|-------------|
| `Content-Type` | `application/json` |
| `x-api-client` | Client ID (configuración) |
| `x-api-secret` | Secret (configuración) |

### Credenciales

Las credenciales **no** deben versionarse en el repositorio. Deben configurarse en:

```text
appsettings.Production.local.json
```

(sección dedicada, mismo criterio que las demás APIs del worker).

### Ejemplo de llamada

```bash
curl --location --request POST 'https://api-radicacion.helpharma.com.co/api/physical-supports/integrations/busqueda' \
--header 'Content-Type: application/json' \
--header 'x-api-client: <valor-desde-config>' \
--header 'x-api-secret: <valor-desde-config>' \
--data '{
    "fecha":"2025-07-02",
    "bodega":"FARMACIAMEDELLIN"
}'
```

---

## 1.4 Datos que debe enviar

### Origen de los datos

Solo participan PDFs con **`Procesado = 1`**. Si un lote termina con PDFs en `procesados` y otros en `noprocesados`, **solo los exitosos** aportan combinaciones.

Un documento con `Procesado = 1` implica que **`FechaFactura`** e **`IdBodega`** están completos (provienen de la respuesta de DatosSoportes al procesar el PDF).

### Mapeo de campos

| Campo del API | Origen | Formato |
|--------------|--------|---------|
| `fecha` | `FechaFactura` | Solo fecha: `yyyy-MM-dd` (ej. `"2025-07-02"`) |
| `bodega` | `IdBodega` | Texto tal cual viene de DatosSoportes (ej. `"FARMACIAMEDELLIN"`) |

> **`IdBodega` y `bodega` son el mismo dato.** En el JSON del API se envía como `"bodega"`.

### Payload exacto

```json
{"fecha":"2025-07-02","bodega":"FARMACIAMEDELLIN"}
```

---

## 1.5 Acumulación en memoria (por lote)

Durante el procesamiento del lote (intentos 1, 2 y 3), por cada PDF **exitoso** (`Procesado = 1`):

1. Tomar `FechaFactura` (parte date) e `IdBodega`.
2. Agregar la pareja a una estructura en memoria (ej. `HashSet`) asociada al lote.
3. **Deduplicar** por `(FechaFactura, bodega)` dentro del mismo lote.

Ejemplo — entradas repetidas:

```text
2026-07-02 - FARMACIAMEDELLIN
2026-07-02 - FARMACIAMEDELLIN
2026-07-02 - FARMACIAMEDELLIN
```

Resultado en memoria:

```text
2026-07-02 - FARMACIAMEDELLIN
```

> No se requiere consulta adicional a BD para armar las combinaciones; los datos se acumulan en memoria mientras se procesan los PDFs.

### Lote sin combinaciones

Si al cerrar el lote **no hay ninguna combinación** (ningún PDF con `Procesado = 1` y datos completos), **no se llama** al API RadicaWeb.

---

# 2. Respuestas del API

## 2.1 Respuesta exitosa

```json
{
    "success": true,
    "message": "Búsqueda de soporte físico encolada exitosamente",
    "solicitudId": 44,
    "registrosInsertados": 8,
    "totalRegistros": 8,
    "jobId": "ms-search-physical-supports-busqueda-44-1783081179470"
}
```

## 2.2 Respuesta de error

```json
{
    "statusCode": 400,
    "message": "Ya se ejecutó hoy una búsqueda para la bodega FARMACIAMEDELLIN con días que se solapan con [2026-07-02 - 2026-07-02]. Conflictos: solicitud #44 [2026-07-01 - 2026-07-01]. No se puede repetir el mismo rango de factura en el mismo día.",
    "error": "Bad Request",
    "timestamp": "2026-07-03T12:20:49.281Z",
    "path": "/api/physical-supports/integrations/busqueda"
}
```

## 2.3 Reglas de manejo de respuestas

- **Siempre** persistir en BD la respuesta recibida (éxito o error).
- **No** discriminar ni tomar acciones distintas según el tipo de respuesta (incluido el 400 por duplicado).
- Tras registrar cada respuesta, **continuar** con la siguiente combinación.
- Un fallo de RadicaWeb (timeout, 500, 400, etc.) **no altera** el resto del ciclo del lote (limpieza, borrado del TXT, contadores, carpetas de PDFs).

---

# 3. Nueva tabla de trazabilidad

Se debe crear una nueva tabla:

```text
RadicaWebAPI
```

Relación obligatoria:

```text
RadicaWebAPI.UsuarioId → Usuario.UsuarioId
```

El `UsuarioId` corresponde al **usuario del lote** (ej. `dgutierrez`, derivado del TXT y la ruta UNC).

## Campos a almacenar

Además de los datos enviados al API, se guarda **toda la respuesta** recibida:

| Campo | Descripción |
|--------|-------------|
| UsuarioId | FK hacia la tabla `Usuario` |
| FechaFactura | Fecha enviada al endpoint (`fecha` del payload) |
| Bodega | Bodega enviada al endpoint (`bodega` del payload) |
| Success | Valor retornado por el API |
| Message | Mensaje retornado (éxito o error) |
| SolicitudId | Id de la solicitud |
| RegistrosInsertados | Cantidad de registros insertados |
| TotalRegistros | Total de registros |
| JobId | Identificador del proceso |
| StatusCode | Código HTTP (especialmente en errores) |
| Error | Tipo de error |
| Timestamp | Fecha y hora de la respuesta del API |
| Path | Endpoint invocado |
| CreadoEn | Fecha y hora de creación del registro en BD |

> **Nota:** El campo **Message** almacena tanto el mensaje de éxito como el de error, según corresponda.

---

# 4. Procesamiento de múltiples facturas por lote

Un único lote (TXT) en `ArchivosNuevos` puede referenciar **múltiples PDFs** con:

- Diferentes fechas de factura.
- Diferentes bodegas.

Las combinaciones se acumulan **por lote (TXT)**, no por PDF individual ni por día de escaneo global.

---

# 5. Ejecución del API

Una vez finalizados **todos los reintentos** (intentos 1, 2 y 3, siendo OpenAI el último):

Para cada combinación única de `(FechaFactura, bodega)` en memoria, el worker debe:

1. Construir el payload:

```json
{"fecha":"2026-07-02","bodega":"FARMACIAMEDELLIN"}
```

2. Consumir el endpoint `POST .../physical-supports/integrations/busqueda`.

3. Esperar la respuesta.

4. Registrar la respuesta en **RadicaWebAPI**.

5. Continuar con la siguiente combinación.

> **Importante:**
> - Las solicitudes se envían **una por una** (no en batch).
> - El **orden** entre combinaciones del mismo lote **no importa**.
> - Si no hay combinaciones, no se invoca el API.

---

# 6. Flujo general

```text
Worker monitorea ArchivosNuevos (*.txt)
            │
            ▼
Detecta lote (TXT)
            │
            ▼
Intento 1 — Barcode (procesar)
            │  └─ Por PDF exitoso: acumular (FechaFactura, bodega) en memoria
            ▼
Intento 2 — Reintento (error)
            │  └─ Por PDF exitoso: acumular en memoria
            ▼
Intento 3 — OpenAI (procesaria)  ← último paso de lectura
            │  └─ Por PDF exitoso: acumular en memoria
            ▼
Correo fallo OpenAI (si aplica)
            │
            ▼
¿Incidencia de infraestructura?
            │
      SÍ ──┴──► PendienteReintento (sin RadicaWeb, TXT conservado)
            │
      NO ──┴──►
            ▼
¿Hay combinaciones únicas en memoria?
            │
      NO ──┴──► Omitir RadicaWeb
            │
      SÍ ──┴──► Por cada (fecha, bodega):
            │       ├── Construir payload
            │       ├── POST RadicaWeb
            │       ├── Persistir respuesta en RadicaWebAPI
            │       └── Continuar (sin alterar flujo del lote)
            ▼
Limpiar temporales
            ▼
Eliminar TXT
            ▼
Lote Completado
```

---

# 7. Fases del proyecto

| Fase | Alcance |
|------|---------|
| **Fase 1 (actual)** | Implementación en **MasivosWorker**: servicio HTTP, acumulación en memoria, tabla `RadicaWebAPI`, integración en `LoteProcesamientoService`. |
| **Fase 2 (posterior)** | Portal **GestionArchivosEscaneados**: consulta/visualización de solicitudes RadicaWeb (cuando se indique). |
