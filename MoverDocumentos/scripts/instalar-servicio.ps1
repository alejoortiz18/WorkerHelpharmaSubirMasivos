#Requires -RunAsAdministrator
<#
.SYNOPSIS
    Instala o actualiza el servicio Windows MoverDocumentos (Worker 1).

.NOTES
    Ejecutar desde PowerShell como Administrador.
    Ruta de publicación: misma carpeta que este script.
#>
$ErrorActionPreference = 'Stop'

$installDir = $PSScriptRoot
$exe = Join-Path $installDir 'MoverDocumentos.exe'
$serviceName = 'MoverDocumentos'
$displayName = 'Helpharma Mover Documentos (Worker 1)'
$description = 'Worker 1: mueve PDFs de C:\scaneo a la NAS UNC y genera lotes TXT para MasivosWorker.'

if (-not (Test-Path $exe)) {
    throw "No se encontró $exe. Publique el proyecto antes de instalar el servicio."
}

if (-not [System.Diagnostics.EventLog]::SourceExists($serviceName)) {
    New-EventLog -LogName Application -Source $serviceName
    Write-Host "Origen de eventos '$serviceName' creado."
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Servicio existente detectado. Deteniendo..."
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    sc.exe config $serviceName binPath= "`"$exe`"" | Out-Null
    sc.exe config $serviceName start= auto | Out-Null
    sc.exe description $serviceName $description | Out-Null
    Write-Host "Servicio actualizado (binPath -> $exe)."
}
else {
    sc.exe create $serviceName binPath= "`"$exe`"" start= auto DisplayName= "$displayName" | Out-Null
    sc.exe description $serviceName $description | Out-Null
    Write-Host "Servicio '$serviceName' creado."
}

$regEnv = "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName\Environment"
if (-not (Test-Path $regEnv)) {
    New-Item -Path $regEnv -Force | Out-Null
}
New-ItemProperty -Path $regEnv -Name 'DOTNET_ENVIRONMENT' -Value 'Production' -PropertyType String -Force | Out-Null
New-ItemProperty -Path $regEnv -Name 'ASPNETCORE_ENVIRONMENT' -Value 'Production' -PropertyType String -Force | Out-Null

Write-Host "Iniciando servicio..."
Start-Service -Name $serviceName
Start-Sleep -Seconds 2
$svc = Get-Service -Name $serviceName
Write-Host "Estado: $($svc.Status)"
Write-Host ""
Write-Host "Logs: Visor de eventos -> Registros de Windows -> Aplicacion -> origen MoverDocumentos"
Write-Host "Config: $installDir\appsettings.json"
