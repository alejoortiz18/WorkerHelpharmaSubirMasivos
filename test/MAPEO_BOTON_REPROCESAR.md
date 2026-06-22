# Mapeo de Funcionalidad - Botón "Reprocesar"

## 📋 Resumen Ejecutivo

El botón **"Reprocesar"** permite volver a procesar automáticamente todos los documentos no procesados intentando detectar sus códigos de barras sin necesidad de entrada manual del usuario.

**Estados del botón:**
- ✅ **Habilitado**: Cuando hay documentos no procesados en la lista
- ❌ **Deshabilitado**: Cuando la lista está vacía

---

## 🏗️ Arquitectura del Flujo

```
┌─────────────────────────────────────────────────────────────────┐
│               INTERFAZ DE USUARIO (NoProcesados.cshtml)           │
├─────────────────────────────────────────────────────────────────┤
│  Botón "Reprocesar" (id=btnReprocesar)                           │
│  ├─ HTML: <button class="btn btn-outline-primary">              │
│  ├─ Estado: disabled={true/false}                               │
│  └─ onClick → Dispara evento click                              │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│         JAVASCRIPT CLIENT-SIDE (NoProcesados.cshtml - Scripts)    │
├─────────────────────────────────────────────────────────────────┤
│ btnReprocesar.addEventListener('click', async () => { ... })    │
│  ├─ 1️⃣ Deshabilita el botón y campos de entrada                │
│  ├─ 2️⃣ Muestra barra de progreso                               │
│  ├─ 3️⃣ Itera sobre cada fila de documento                      │
│  ├─ 4️⃣ Para cada documento:                                     │
│  │   └─ Envía POST a /Documentos/ReprocesarDocumento             │
│  ├─ 5️⃣ Actualiza barra de progreso (X/Total)                   │
│  └─ 6️⃣ Recarga la página (window.location.reload)              │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│    API CONTROLLER (DocumentosController.ReprocesarDocumento)     │
├─────────────────────────────────────────────────────────────────┤
│ [HttpPost] /Documentos/ReprocesarDocumento                       │
│  ├─ Extrae usuario de sesión                                    │
│  ├─ Extrae parámetros: Fecha, NombreArchivo                    │
│  ├─ Valida que NombreArchivo no sea nulo/vacío                 │
│  ├─ Llama: await _reproceso.ReprocesarAsync(...)               │
│  └─ Retorna JSON: { exito: bool, estado: string }              │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│      APPLICATION SERVICE (ReprocesoAppService.ReprocesarAsync)    │
├─────────────────────────────────────────────────────────────────┤
│ public async Task<SoporteProcesamientoEstado> ReprocesarAsync() │
│  ├─ 📝 LOG: "ReprocesoInicio"                                   │
│  ├─ 1️⃣ Resuelve ruta segura del PDF:                           │
│  │   └─ _unc.ResolverRutaPdfSegura(usuario, fecha, archivo)     │
│  ├─ 2️⃣ Valida que el archivo existe                            │
│  ├─ 3️⃣ **Intenta leer código de barras del PDF:**              │
│  │   └─ await LeerCodigoBarrasAsync(rutaPdf)                    │
│  │      (con reintentos configurables)                          │
│  ├─ ➡️ SI se encuentra barcode:                                 │
│  │   └─ Usa el código detectado                                 │
│  ├─ ❌ SI NO se encuentra barcode:                              │
│  │   ├─ 📝 LOG: "ReprocesoBarcodeNoDetectado"                  │
│  │   ├─ **Envía a OpenAI para detección fallback**             │
│  │   └─ Espera resultado del servicio                           │
│  ├─ 4️⃣ **Envía documento procesado a API de Soporte:**         │
│  │   └─ await _soporte.ProcesarAsync(                           │
│  │         codigoDetectado, pdf, archivo, usuario)              │
│  ├─ 5️⃣ Si tiene éxito, marca documento como procesado:         │
│  │   └─ await _trazabilidad.MarcarDocumentoProcesadoAsync()    │
│  ├─ 6️⃣ Mueve archivo de carpeta "noProcesados" a "procesados": │
│  │   └─ _unc.MoverANoprocesadosAProcesados()                   │
│  ├─ 📝 LOG: "ReprocesoExitoso"                                  │
│  └─ Retorna: SoporteProcesamientoEstado (Exito/Error)          │
└─────────────────────────────────────────────────────────────────┘
```

---

## 🔄 Flujo Detallado del Botón "Reprocesar"

### **Fase 1: Inicialización (JavaScript - Client)**

```javascript
btnReprocesar.addEventListener('click', async () => {
    // 1. Obtiene todas las filas de documentos
    const reprocessRows = rows;  // Array de elementos TR.docs-row
    const total = reprocessRows.length;
    
    // 2. Valida que hay documentos
    if (total === 0) return;
    
    // 3. Deshabilita botones y campos
    btnReprocesar.disabled = true;
    btnProcesar.disabled = true;
    form.querySelectorAll('.barcode-input').forEach(input => 
        input.disabled = true
    );
    
    // 4. Muestra barra de progreso
    progressCard.classList.remove('d-none');
    reprocessInlineStatus.textContent = 'Procesando archivos...';
    reprocessInlineStatus.classList.remove('d-none');
    updateProgress(0, total);
});
```

### **Fase 2: Procesamiento en Lote (JavaScript - Client)**

```javascript
// Itera sobre cada documento y envía al servidor
let done = 0;
for (const row of reprocessRows) {
    // Llama reprocesarFila con índice actual
    await reprocesarFila(row, done + 1, total);
    done++;
    // Actualiza barra de progreso: muestra "2/3", "3/3", etc.
    updateProgress(done, total);
}

// Recarga la página con documentos actualizados
setTimeout(() => window.location.reload(), 500);
```

### **Fase 3: Envío de Cada Documento (JavaScript - Client)**

```javascript
async function reprocesarFila(row, index, total) {
    const nombreArchivo = row.dataset.archivo ?? '';
    if (!nombreArchivo) {
        return { skipped: true };
    }
    
    // Obtiene token CSRF y parámetros
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    const payload = new FormData();
    payload.append('__RequestVerificationToken', token);
    payload.append('Fecha', document.querySelector('input[name="Fecha"]')?.value ?? '');
    payload.append('NombreArchivo', nombreArchivo);
    
    // Envía POST a: /Documentos/ReprocesarDocumento
    const response = await fetch(reprocessUrl, {
        method: 'POST',
        body: payload
    });
    
    // Parsea respuesta JSON
    const result = await response.json();
    
    // Actualiza progreso
    updateProgress(index, total);
    
    return { 
        skipped: false, 
        estado: result.estado, 
        exito: !!result.exito 
    };
}
```

### **Fase 4: Procesamiento en Servidor (C# - Controller)**

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> ReprocesarDocumento(
    ReprocesarDocumentoRequest request, 
    CancellationToken cancellationToken)
{
    // 1. Obtiene usuario de la sesión
    var usuario = HttpContext.Session.GetString(SessionKeys.Usuario)!;
    
    // 2. Valida entrada
    if (string.IsNullOrWhiteSpace(request.NombreArchivo))
    {
        return BadRequest(new
        {
            exito = false,
            estado = SoporteProcesamientoEstado.ErrorInesperado.ToString()
        });
    }
    
    // 3. Llama al servicio de aplicación
    var estado = await _reproceso.ReprocesarAsync(
        usuario,
        request.Fecha,
        request.NombreArchivo,
        string.Empty,  // codigoBarras vacío en reproceso automático
        cancellationToken);
    
    // 4. Retorna respuesta JSON
    return Json(new
    {
        exito = estado == SoporteProcesamientoEstado.Exito,
        estado = estado.ToString()
    });
}
```

### **Fase 5: Lógica de Reproceso (C# - ReprocesoAppService)**

```csharp
public async Task<SoporteProcesamientoEstado> ReprocesarAsync(
    string usuario,
    string fecha,
    string nombreArchivo,
    string codigoBarras,
    CancellationToken cancellationToken = default)
{
    // 📝 LOGGING
    _logger.LogInformation(
        "ReprocesoInicio | Usuario={Usuario} | Fecha={Fecha} | Archivo={Archivo}",
        usuario, fecha, nombreArchivo);
    
    // ✅ PASO 1: Resolver ruta segura del PDF
    var rutaPdf = _unc.ResolverRutaPdfSegura(usuario, fecha, nombreArchivo);
    if (string.IsNullOrWhiteSpace(rutaPdf) || !File.Exists(rutaPdf))
    {
        _logger.LogWarning(
            "ReprocesoPdfNoEncontrado | Usuario={Usuario} | Fecha={Fecha} | Archivo={Archivo}",
            usuario, fecha, nombreArchivo);
        return SoporteProcesamientoEstado.FalloApiDatos;
    }
    
    // ✅ PASO 2: Intenta detectar código de barras con IronBarCode
    var codigoDetectado = await LeerCodigoBarrasAsync(rutaPdf, cancellationToken);
    
    // ✅ PASO 3: Si no se detecta, usa OpenAI como fallback
    if (string.IsNullOrWhiteSpace(codigoDetectado))
    {
        _logger.LogInformation(
            "ReprocesoBarcodeNoDetectado | Archivo={Archivo} | Accion=EnviarOpenAI",
            nombreArchivo);
        
        // 🤖 Llamada a OpenAI (gpt-4.1-mini)
        var resultadoOpenAi = await _openAiBarcodeService.LeerCodigoAsync(
            rutaPdf, cancellationToken);
        
        _logger.LogInformation(
            "ReprocesoOpenAiResultado | Archivo={Archivo} | Tipo={Tipo} | Codigo={Codigo}",
            nombreArchivo, resultadoOpenAi.Tipo, resultadoOpenAi.Codigo ?? "-");
        
        // Maneja resultado de OpenAI
        switch (resultadoOpenAi.Tipo)
        {
            case OpenAiBarcodeResultKind.CodigoEncontrado:
                codigoDetectado = resultadoOpenAi.Codigo;
                break;
            case OpenAiBarcodeResultKind.NoBarcode:
                return SoporteProcesamientoEstado.FalloBarcode;
            case OpenAiBarcodeResultKind.ErrorServicio:
            default:
                return SoporteProcesamientoEstado.FalloOpenAi;
        }
    }
    else
    {
        _logger.LogInformation(
            "ReprocesoBarcodeDetectado | Archivo={Archivo} | Codigo={Codigo}",
            nombreArchivo, codigoDetectado);
    }
    
    // ✅ PASO 4: Valida que se encontró código
    if (string.IsNullOrWhiteSpace(codigoDetectado))
        return SoporteProcesamientoEstado.FalloBarcode;
    
    // ✅ PASO 5: Lee contenido del PDF
    var pdf = await File.ReadAllBytesAsync(rutaPdf, cancellationToken);
    
    _logger.LogInformation(
        "ReprocesoEnviarSoporte | Archivo={Archivo} | Codigo={Codigo} | Bytes={Bytes}",
        nombreArchivo, codigoDetectado, pdf.Length);
    
    // ✅ PASO 6: Envía a API de Soporte (backend externo)
    var resultado = await _soporte.ProcesarAsync(
        codigoDetectado,
        pdf,
        nombreArchivo,
        usuario,
        cancellationToken);
    
    // Maneja respuesta de Soporte
    if (!resultado.EsExitoso)
    {
        _logger.LogWarning(
            "ReprocesoSoporteFallo | Archivo={Archivo} | Codigo={Codigo} | Estado={Estado}",
            nombreArchivo, codigoDetectado, resultado.Estado);
        return resultado.Estado;
    }
    
    // ✅ PASO 7: Marca documento como procesado en BD
    var actualizado = await _trazabilidad.MarcarDocumentoProcesadoAsync(
        usuario,
        fecha,
        nombreArchivo,
        resultado.Soporte,
        resultado.Datos?.IdPaciente,
        cancellationToken);
    
    if (!actualizado)
        return SoporteProcesamientoEstado.ErrorInesperado;
    
    // ✅ PASO 8: Mueve archivo de carpeta noProcesados → procesados
    var rutas = _unc.ObtenerRutasDia(usuario, fecha);
    var ruta = Path.Combine(rutas.Noprocesados, nombreArchivo);
    _unc.MoverANoprocesadosAProcesados(ruta, rutas);
    
    _logger.LogInformation(
        "ReprocesoExitoso | Archivo={Archivo} | Codigo={Codigo}",
        nombreArchivo, codigoDetectado);
    
    // ✅ ÉXITO
    return SoporteProcesamientoEstado.Exito;
}
```

### **Fase 6: Detección de Código de Barras (IronBarCode con Reintentos)**

```csharp
private async Task<string?> LeerCodigoBarrasAsync(
    string rutaPdf,
    CancellationToken cancellationToken)
{
    var settings = _fileSettings.Value;
    var maxReintentos = Math.Max(1, settings.BarcodeMaxReintentos);
    var esperaMs = Math.Max(100, settings.BarcodeEsperaMs);
    
    // Intenta múltiples veces (con espera entre intentos)
    for (var intento = 1; intento <= maxReintentos; intento++)
    {
        cancellationToken.ThrowIfCancellationRequested();
        
        try
        {
            // Corre en thread separado para no bloquear
            var codigo = await Task.Run(
                () => _barcodeRegionService.LeerCodigoDesdePdf(rutaPdf),
                cancellationToken);
            
            if (!string.IsNullOrWhiteSpace(codigo))
                return codigo;  // ✅ Código encontrado
        }
        catch (Exception)
        {
            if (intento >= maxReintentos)
                return null;  // ❌ Agotó reintentos
        }
        
        // Espera antes del siguiente reintento
        if (intento < maxReintentos)
        {
            await Task.Delay(esperaMs, cancellationToken);
        }
    }
    
    return null;  // ❌ No se pudo detectar
}
```

---

## 📊 Estados Posibles de Retorno

| Estado | Código | Significado | Acción |
|--------|--------|-------------|--------|
| **Exito** | ✅ | Documento procesado correctamente | Documento pasa a "Procesados" |
| **FalloBarcode** | ❌ | No se detectó código de barras | Permanece en "No Procesados" |
| **FalloOpenAi** | ❌ | Error llamando a OpenAI | Permanece en "No Procesados" |
| **FalloApiDatos** | ❌ | PDF no encontrado o no es legible | Permanece en "No Procesados" |
| **ErrorInesperado** | ❌ | Error general (BD, archivo, etc.) | Permanece en "No Procesados" |

---

## 🔍 Componentes Involucrados

### **Servicios:**
- `ReprocesoAppService` → Orquesta la lógica de reproceso
- `UncStorageService` → Acceso a archivos en UNC
- `IBarcodeRegionService` → Detección con IronBarCode
- `IOpenAiBarcodeService` → Fallback a OpenAI gpt-4.1-mini
- `ISoporteProcesamientoService` → API externa de procesamiento
- `ITrazabilidadConsultaSqlService` → Actualización BD

### **Interfaces:**
- `ReprocesarDocumentoRequest` → Parámetros POST
- `SoporteProcesamientoEstado` → Enum de estados

### **Archivos:**
- `NoProcesados.cshtml` → Interfaz HTML
- `DocumentosController.cs` → Endpoints HTTP
- `ApplicationServices.cs` → Lógica de negocio

---

## ⏱️ Secuencia Temporal

```
T+0ms   → Usuario hace click en botón "Reprocesar"
T+10ms  → JavaScript deshabilita botones/campos
T+15ms  → Muestra barra de progreso 0/3
T+20ms  → Envía primer documento al servidor (POST)
T+50ms  → Servidor: lee PDF, detecta barcode con IronBarCode
T+100ms → Servidor: envía a OpenAI (si falla detección local)
T+300ms → Servidor: OpenAI retorna código
T+350ms → Servidor: envía a API Soporte
T+400ms → Servidor: marca BD como procesado
T+420ms → Servidor: mueve archivo (noProcesados → procesados)
T+430ms → Servidor: retorna JSON { exito: true, estado: "Exito" }
T+440ms → JavaScript actualiza progreso 1/3
T+450ms → JavaScript envía segundo documento...
T+900ms → JavaScript envía tercer documento...
T+1400ms→ Todos los documentos procesados
T+1900ms→ JavaScript recarga página (window.location.reload())
T+2000ms→ Página se recarga con lista actualizada
```

---

## 🎯 Casos de Uso y Resultados

### **Caso 1: Éxito Completo**
```
Archivo: FPE51028.pdf
├─ Detección IronBarCode: ✅ Detecta "ME-12345"
├─ Envío a Soporte: ✅ Procesa correctamente
├─ BD actualizada: ✅ Marcado como procesado
└─ Resultado: ÉXITO (documento pasa a "Procesados")
```

### **Caso 2: Detección Fallida, OpenAI Rescata**
```
Archivo: FPE51030.pdf
├─ Detección IronBarCode: ❌ No detecta
├─ Fallback OpenAI: ✅ Detecta "HE-98765"
├─ Envío a Soporte: ✅ Procesa correctamente
└─ Resultado: ÉXITO (documento pasa a "Procesados")
```

### **Caso 3: OpenAI También Falla**
```
Archivo: FPE51032.pdf
├─ Detección IronBarCode: ❌ No detecta
├─ Fallback OpenAI: ❌ OpenAI retorna "NoBarcode"
└─ Resultado: FALLO - FalloBarcode (permanece en "No Procesados")
```

### **Caso 4: PDF Dañado o No Existe**
```
Archivo: corrupted.pdf
├─ Resolución ruta: ✅ Encuentra ruta
├─ Validación archivo: ❌ Archivo no existe o dañado
└─ Resultado: FALLO - FalloApiDatos (permanece en "No Procesados")
```

---

## 💾 Datos Modificados en BD

Cuando el reproceso es exitoso, se actualiza:

```sql
-- Tabla: dbo.DocumentosProcesados
UPDATE DocumentosProcesados
SET 
    Codigo = 'ME-12345',                    -- Código detectado
    IdSoporte = 12345,                       -- ID retornado por API
    IdPaciente = 'PAC-001',                  -- Paciente asociado
    FechaProcesamiento = GETDATE(),          -- Timestamp de procesamiento
    Estado = 'Procesado',                    -- Estado actualizado
    TieneIntentoPrevio = 1                   -- Marca que fue reprocesado
WHERE Usuario = 'alejandro.ortiz'
    AND Fecha = '2026-06-17'
    AND Archivo = 'FPE51028.pdf'

-- Tabla: dbo.LogsDiarios (incrementa contador)
UPDATE LogsDiarios
SET 
    Procesados = Procesados + 1,             -- Incrementa contador
    UltimoProcesamiento = GETDATE()
WHERE Usuario = 'alejandro.ortiz'
    AND FechaProcesamiento = '2026-06-17'
```

---

## 🔐 Seguridad y Validaciones

✅ **Validaciones implementadas:**
1. Verificación de CSRF token (`ValidateAntiForgeryToken`)
2. Extracción de usuario desde sesión (no desde parámetro)
3. Resolución segura de rutas (`ResolverRutaPdfSegura`)
4. Validación de existencia de archivo
5. Control de acceso por usuario (LM filtrado por usuario)

❌ **Riesgos mitigados:**
- No se puede procesar archivos de otros usuarios
- No se puede especificar rutas arbitrarias
- No se puede inyectar códigos de barras falsos
- Todas las transacciones registradas en logs

---

## 📝 Información de Logging

El sistema registra en logs cada fase:

```
ReprocesoInicio               → Inicio del procesamiento
ReprocesoPdfNoEncontrado      → PDF no encontrado
ReprocesoBarcodeNoDetectado   → Barcode no detectable, enviando a OpenAI
ReprocesoOpenAiResultado      → Resultado de OpenAI
ReprocesoBarcodeDetectado     → Barcode detectado localmente
ReprocesoEnviarSoporte        → Enviando a API de Soporte
ReprocesoSoporteFallo         → API de Soporte retorna error
ReprocesoExitoso              → Documento procesado exitosamente
```

---

## 🚀 Conclusión

El botón "Reprocesar" automatiza completamente el procesamiento de documentos mediante:

1. **Detección automática de códigos** (IronBarCode)
2. **Fallback inteligente** (OpenAI gpt-4.1-mini)
3. **Integración con API externa** (Soporte)
4. **Actualización de BD** y movimiento de archivos
5. **Refresco automático** de la interfaz
6. **Barra de progreso** en tiempo real

**Valor agregado:** El usuario no necesita ingresar códigos manualmente; el sistema intenta detectarlos automáticamente.
