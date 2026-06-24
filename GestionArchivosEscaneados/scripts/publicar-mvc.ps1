# Publica el portal MVC y conserva credenciales NAS en la carpeta destino.
# Uso (PowerShell):
#   .\scripts\publicar-mvc.ps1
#   .\scripts\publicar-mvc.ps1 -Destino "D:\sitios\GestionArchivosEscaneados"

param(
    [string]$Destino = "C:\Users\alejandro.ortiz\Documents\helpharma\Desarrollos\publicaciones\Soporte Masivos\MVC",
    [string]$CredencialesLocales = "$PSScriptRoot\appsettings.Production.local.json"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$webProject = Join-Path $repoRoot "GestionArchivosEscaneados.Web\GestionArchivosEscaneados.Web.csproj"

Write-Host "Publicando MVC en: $Destino"
$redAnterior = $null
$appsettingsDestino = Join-Path $Destino "appsettings.json"
if (Test-Path $appsettingsDestino) {
    $redAnterior = (Get-Content $appsettingsDestino -Raw | ConvertFrom-Json).Red
}

dotnet publish $webProject -c Release -o $Destino

New-Item -ItemType Directory -Force -Path (Join-Path $Destino "logs") | Out-Null

$config = Get-Content (Join-Path $Destino "appsettings.json") -Raw | ConvertFrom-Json
if (Test-Path $CredencialesLocales) {
    $local = Get-Content $CredencialesLocales -Raw | ConvertFrom-Json
    if ($local.Red) { $config.Red = $local.Red }
    Write-Host "Credenciales NAS aplicadas desde appsettings.Production.local.json"
}
elseif ($redAnterior -and $redAnterior.UsarCredencialesConfiguradas) {
    $config.Red = $redAnterior
    Write-Host "Credenciales NAS conservadas del despliegue anterior"
}
else {
    Write-Warning "Sin credenciales NAS. Cree $CredencialesLocales o edite appsettings.json en destino."
}

$config | ConvertTo-Json -Depth 10 | Set-Content $appsettingsDestino -Encoding UTF8

$promptsOrigen = Join-Path $repoRoot "GestionArchivosEscaneados.Web\Prompts"
$promptsDestino = Join-Path $Destino "Prompts"
New-Item -ItemType Directory -Force -Path $promptsDestino | Out-Null
Copy-Item (Join-Path $promptsOrigen "*") $promptsDestino -Recurse -Force
Write-Host "Prompts OpenAI copiados a $promptsDestino"

Write-Host ""
Write-Host "Listo. Pasos en IIS (como administrador):"
Write-Host "  1. Sitio -> Ruta fisica = $Destino"
Write-Host "  2. App Pool -> Reciclar"
Write-Host "  3. Permisos escritura en $Destino\logs para IIS_IUSRS"
Write-Host "  4. Revisar $Destino\logs\stdout_*.log -> StartupUnc Accesible=True"
