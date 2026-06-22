# Publica Worker 2 y aplica credenciales NAS desde appsettings.Production.local.json
param(
    [string]$Destino = "C:\Users\alejandro.ortiz\Documents\helpharma\Desarrollos\publicaciones\Soporte Masivos\Worker2",
    [string]$DestinoServicio = "C:\servicios\Worker2",
    [string]$CredencialesLocales = "$PSScriptRoot\appsettings.Production.local.json"
)

$ErrorActionPreference = "Stop"
$project = Join-Path (Split-Path $PSScriptRoot -Parent) "MasivosWorker\MasivosWorker.csproj"

function Test-TieneTexto($valor) {
    return -not [string]::IsNullOrWhiteSpace([string]$valor)
}

function Merge-Seccion($config, $origen, [string]$nombreSeccion) {
    if ($origen -and $origen.PSObject.Properties.Name -contains $nombreSeccion) {
        $valor = $origen.$nombreSeccion
        if ($null -ne $valor) {
            $config.$nombreSeccion = $valor
        }
    }
}

function Get-FaltantesConfiguracion($config) {
    $faltantes = New-Object System.Collections.Generic.List[string]

    if (-not (Test-TieneTexto $config.Red.Usuario)) { $faltantes.Add("Red.Usuario") }
    if (-not (Test-TieneTexto $config.Red.Clave)) { $faltantes.Add("Red.Clave") }
    if (-not (Test-TieneTexto $config.OpenAi.ApiKey)) { $faltantes.Add("OpenAi.ApiKey") }
    if (-not (Test-TieneTexto $config.Email.Remitente)) { $faltantes.Add("Email.Remitente") }
    if (-not (Test-TieneTexto $config.Email.SmtpHost)) { $faltantes.Add("Email.SmtpHost") }
    if (-not (Test-TieneTexto $config.Email.Usuario)) { $faltantes.Add("Email.Usuario") }
    if (-not (Test-TieneTexto $config.Email.Clave)) { $faltantes.Add("Email.Clave") }

    $destinatarios = @($config.Email.Destinatarios | Where-Object { Test-TieneTexto $_ })
    if ($destinatarios.Count -eq 0) { $faltantes.Add("Email.Destinatarios") }

    return $faltantes
}

Get-Process -Name "MasivosWorker" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

$configAnterior = $null
if (Test-Path (Join-Path $Destino "appsettings.json")) {
    $configAnterior = Get-Content (Join-Path $Destino "appsettings.json") -Raw | ConvertFrom-Json
}

dotnet publish $project -c Release -o $Destino

$config = Get-Content (Join-Path $Destino "appsettings.json") -Raw | ConvertFrom-Json
Merge-Seccion $config $configAnterior "Red"
Merge-Seccion $config $configAnterior "Email"
Merge-Seccion $config $configAnterior "OpenAi"
Merge-Seccion $config $configAnterior "IronBarcode"
Merge-Seccion $config $configAnterior "ApiCredentials"

if (Test-Path $CredencialesLocales) {
    $local = Get-Content $CredencialesLocales -Raw | ConvertFrom-Json
    Merge-Seccion $config $local "Red"
    Merge-Seccion $config $local "Email"
    Merge-Seccion $config $local "OpenAi"
    Merge-Seccion $config $local "IronBarcode"
    Merge-Seccion $config $local "ApiCredentials"
    Copy-Item $CredencialesLocales (Join-Path $Destino "appsettings.Production.local.json") -Force
}

$faltantes = Get-FaltantesConfiguracion $config
if ($faltantes.Count -gt 0) {
    $detalleFaltantes = ($faltantes | ForEach-Object { " - $_" }) -join [Environment]::NewLine
    $mensaje = @(
        "La publicacion se detuvo porque faltan configuraciones obligatorias para Worker 2:",
        $detalleFaltantes,
        "",
        "Complete MasivosWorker\scripts\appsettings.Production.local.json o reutilice una publicacion previa con esos valores."
    ) -join [Environment]::NewLine

    throw $mensaje
}

$config | ConvertTo-Json -Depth 10 | Set-Content (Join-Path $Destino "appsettings.json") -Encoding UTF8
Write-Host "Worker 2 publicado en: $Destino"

if ($DestinoServicio -and (Test-Path $DestinoServicio)) {
    robocopy $Destino $DestinoServicio /MIR /NFL /NDL /NJH /NJS /NC /NS | Out-Null
    Write-Host "Copiado a servicio: $DestinoServicio"
}
elseif ($DestinoServicio) {
    New-Item -ItemType Directory -Path $DestinoServicio -Force | Out-Null
    robocopy $Destino $DestinoServicio /E /NFL /NDL /NJH /NJS /NC /NS | Out-Null
    Copy-Item (Join-Path $PSScriptRoot "instalar-servicio.ps1") (Join-Path $DestinoServicio "instalar-servicio.ps1") -Force
    Write-Host "Copiado a servicio: $DestinoServicio"
    Write-Host "Ejecute como admin: $DestinoServicio\instalar-servicio.ps1"
}
