# MoverDocumentos (Worker 1)

Servicio Windows que escucha PDFs en `C:\scaneo`, los mueve a `\\192.168.0.69\ArchivosScaneados` y genera un `.txt` por lote en `\\192.168.0.69\ArchivosScaneados\ArchivosNuevos` (una línea con la ruta UNC de la carpeta `procesar`) para **MasivosWorker**.

## Rutas de producción

| Uso | Ruta |
|-----|------|
| Entrada (escáner) | `C:\scaneo` |
| Raíz red | `\\192.168.0.69\ArchivosScaneados` |
| PDFs del día | `\\192.168.0.69\ArchivosScaneados\{usuario}\{fecha}\procesar` |
| Señal de lote | `\\192.168.0.69\ArchivosScaneados\ArchivosNuevos\*.txt` |
| Usuarios | `\\192.168.0.69\ArchivosScaneados\Usuarios\usuarios.txt` |

Configuración en `MoverDocumentos/appsettings.json`.

## Ejecutar

```powershell
cd MoverDocumentos\MoverDocumentos
dotnet run
```

Como servicio Windows: ver [INSTALACION.md](INSTALACION.md).

## Pruebas unitarias

```powershell
dotnet test
```

(Las pruebas usan carpetas temporales; no modifican `C:\scaneo` ni la UNC.)
