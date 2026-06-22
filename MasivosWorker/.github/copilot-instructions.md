# MasivosWorker – Instrucciones del Agente

Este es el proyecto **MasivosWorker**, un Windows Service en .NET 10.0 de Helpharma que procesa soportes físicos masivos.

## Contexto del Proyecto

- **Propósito:** Monitorear carpetas, leer códigos de barras en PDFs con IronBarcode, consultar datos en API de soportes y subir documentos a la intranet de Helpharma.
- **Lenguaje:** C# / .NET 10.0
- **Tipo:** Windows Service (BackgroundService)
- **Solución:** `MasivosWorker.slnx` con proyectos: `MasivosWorker`, `Business`, `Services`, `Infrastructure`, `Models`

## Convenciones de Código

- Nombres de métodos y variables **en español**
- Sufijos de capas: `...Infraestructura`, `...Service`, `...Dto`, `...Settings`
- Todos los servicios como **Singleton** en DI
- Configuración tipada con `IOptions<T>` desde `appsettings.json`
- Async/await en todo el pipeline de procesamiento

## Skills Disponibles

- `/masivos-worker-context` — Arquitectura completa, clases, DTOs, APIs y flujo del proyecto
- `/csharp-analysis` — Análisis técnico de código C# del proyecto

## APIs del Proyecto

- `https://api-soportes.helpharma.com.co` — Consulta datos del soporte (X-API-KEY)
- `https://intranet.helpharma.com/api/v1/soporte/fisico` — Subida de soporte físico (Bearer)
