# Prueba de ejecución en PRODUCCIÓN (C:\scaneo → UNC)
# Uso: powershell -ExecutionPolicy Bypass -File .\scripts\probar-ejecucion.ps1

$ErrorActionPreference = "Stop"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:DOTNET_ENVIRONMENT = "Production"

$repoRoot = Split-Path $PSScriptRoot -Parent
$project = Join-Path $repoRoot "MoverDocumentos\MoverDocumentos.csproj"
$pdfOrigen = Join-Path $repoRoot "..\MasivosWorker\.github\DocumentosTest\CRC_900277244_KV_351697.pdf"

$scaneo = "C:\scaneo"
$raizUnc = "\\192.168.0.69\ArchivosScaneados"
$archivosNuevos = Join-Path $raizUnc "ArchivosNuevos"

Write-Host "=== MoverDocumentos - prueba PRODUCCION ===" -ForegroundColor Cyan

if (-not (Test-Path $pdfOrigen)) {
    throw "No se encontro PDF de prueba: $pdfOrigen"
}

if (-not (Test-Path $raizUnc)) {
    throw "No hay acceso a $raizUnc. Verifique red y permisos."
}

New-Item -ItemType Directory -Path $scaneo -Force | Out-Null

Write-Host "Compilando..."
dotnet build $project -c Release -v minimal
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "Iniciando worker (Production)..."
$runJob = Start-Job -ScriptBlock {
    param($proj)
    $env:DOTNET_ENVIRONMENT = "Production"
    $env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
    Set-Location (Split-Path $proj -Parent)
    dotnet run --project $proj -c Release --no-launch-profile 2>&1
} -ArgumentList $project

Start-Sleep -Seconds 10

$nombrePdf = "Factura-prueba-produccion-$(Get-Date -Format 'HHmmss').pdf"
Write-Host "Copiando PDF a $scaneo ..."
Copy-Item $pdfOrigen (Join-Path $scaneo $nombrePdf)

Write-Host "Esperando movimiento y cierre de lote (75 s)..."
Start-Sleep -Seconds 75

$pendientes = @(Get-ChildItem $scaneo -Filter *.pdf -ErrorAction SilentlyContinue)
$txts = @(Get-ChildItem $archivosNuevos -Filter *.txt -ErrorAction SilentlyContinue |
    Sort-Object LastWriteTime -Descending | Select-Object -First 5)

Write-Host ""
Write-Host "Resultados:" -ForegroundColor Yellow
Write-Host "  PDF en C:\scaneo: $($pendientes.Count) (esperado 0 si ya proceso)"
Write-Host "  TXT recientes en ArchivosNuevos: $($txts.Count)"

if ($txts.Count -gt 0) {
    Write-Host "  Ultimo TXT: $($txts[0].Name)"
    Write-Host "  Contenido:"
    Get-Content $txts[0].FullName | ForEach-Object { Write-Host "    $_" }
}

$ok = ($txts.Count -ge 1) -and ($txts[0].FullName -like "*ArchivosNuevos*")

Stop-Job $runJob -ErrorAction SilentlyContinue
Remove-Job $runJob -Force -ErrorAction SilentlyContinue
Get-Process -Name "dotnet" -ErrorAction SilentlyContinue |
    Where-Object { $_.Path -like "*MoverDocumentos*" } |
    Stop-Process -Force -ErrorAction SilentlyContinue

if ($ok) {
    Write-Host ""
    Write-Host "PRUEBA OK" -ForegroundColor Green
    exit 0
}

Write-Host ""
Write-Host "PRUEBA FALLIDA - logs:" -ForegroundColor Red
Receive-Job $runJob -ErrorAction SilentlyContinue | Select-Object -Last 30
exit 1
