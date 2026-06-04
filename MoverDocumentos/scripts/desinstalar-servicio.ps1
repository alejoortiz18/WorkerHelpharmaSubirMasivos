#Requires -RunAsAdministrator
$ErrorActionPreference = 'Stop'

$serviceName = 'MoverDocumentos'
$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue

if (-not $existing) {
    Write-Host "El servicio '$serviceName' no está instalado."
    exit 0
}

Write-Host "Deteniendo servicio..."
Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 2

Write-Host "Eliminando servicio..."
sc.exe delete $serviceName | Out-Null
Write-Host "Servicio '$serviceName' eliminado."
