<#
    Prueba en vivo de la "busqueda en OpenAI" del MasivosWorker.
    Replica la logica de OpenAiBarcodeService.LeerCodigoAsync contra un PDF real.

    Uso:
        ./test-openai-pdf.ps1 -PdfPath "C:\...\ArchivosTest\CRC_900277244_FE249758.pdf"

    La ApiKey se toma (en orden):
        1) parametro -ApiKey
        2) variable de entorno OPENAI_API_KEY
        3) OpenAi:ApiKey de appsettings.Production.local.json (junto a este script)
#>
param(
    [string]$PdfPath = "C:\Users\serviciosrelease\Documents\Desarrollos\workerHelpharmaSubirArchivos\WorkerHelpharmaSubirMasivos\ArchivosTest\CRC_900277244_FE249758.pdf",
    [string]$ApiKey,
    [string]$Model = "gpt-4.1-mini",
    [int]$TimeoutSeconds = 60,
    [int]$MaxReintentos = 3
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

function Resolve-ApiKey {
    param([string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) { return $Explicit }
    if (-not [string]::IsNullOrWhiteSpace($env:OPENAI_API_KEY)) { return $env:OPENAI_API_KEY }
    $localCfg = Join-Path $scriptDir "appsettings.Production.local.json"
    if (Test-Path $localCfg) {
        $json = Get-Content $localCfg -Raw | ConvertFrom-Json
        if ($json.OpenAi -and -not [string]::IsNullOrWhiteSpace($json.OpenAi.ApiKey)) {
            return $json.OpenAi.ApiKey
        }
    }
    throw "No se encontro ApiKey (parametro, OPENAI_API_KEY, ni appsettings.Production.local.json)."
}

function Resolve-Prompt {
    $promptFile = Join-Path $scriptDir "..\MasivosWorker\Prompts\barcode-openai.txt"
    if (Test-Path $promptFile) {
        return [System.IO.File]::ReadAllText($promptFile, [System.Text.Encoding]::UTF8)
    }
    return "Lee el documento PDF adjunto. Localiza el codigo de barras principal y devuelve el texto legible impreso justo debajo. Responde una sola linea, sin explicaciones. Si no hay codigo, responde NO_BARCODE."
}

if (-not (Test-Path $PdfPath)) { throw "PDF no encontrado: $PdfPath" }

$apiKey = Resolve-ApiKey -Explicit $ApiKey
$prompt = Resolve-Prompt
$fileName = Split-Path -Leaf $PdfPath
$pdfBytes = [System.IO.File]::ReadAllBytes($PdfPath)
$pdfBase64 = [System.Convert]::ToBase64String($pdfBytes)

$body = @{
    model       = $Model
    temperature = 0
    max_tokens  = 32
    messages    = @(
        @{
            role    = "user"
            content = @(
                @{ type = "text"; text = $prompt },
                @{ type = "file"; file = @{ filename = "documento.pdf"; file_data = "data:application/pdf;base64,$pdfBase64" } }
            )
        }
    )
} | ConvertTo-Json -Depth 10

Write-Host "==> Enviando '$fileName' a OpenAI ($Model)..." -ForegroundColor Cyan

Add-Type -AssemblyName System.Net.Http

for ($intento = 1; $intento -le [Math]::Max(1, $MaxReintentos); $intento++) {
    $client = [System.Net.Http.HttpClient]::new()
    $client.Timeout = [TimeSpan]::FromSeconds([Math]::Max(10, $TimeoutSeconds))
    try {
        $req = [System.Net.Http.HttpRequestMessage]::new(
            [System.Net.Http.HttpMethod]::Post, "https://api.openai.com/v1/chat/completions")
        $req.Headers.Authorization =
            [System.Net.Http.Headers.AuthenticationHeaderValue]::new("Bearer", $apiKey)
        $req.Content = [System.Net.Http.StringContent]::new(
            $body, [System.Text.Encoding]::UTF8, "application/json")

        $resp = $client.SendAsync($req).GetAwaiter().GetResult()
        $respBody = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        $statusCode = [int]$resp.StatusCode

        if (-not $resp.IsSuccessStatusCode) {
            Write-Host "Intento $intento fallo. HTTP=$statusCode" -ForegroundColor Red
            Write-Host $respBody -ForegroundColor DarkGray
            if ($statusCode -eq 401) { Write-Host "==> ApiKey invalida o revocada." -ForegroundColor Red; exit 1 }
            if ($intento -lt $MaxReintentos) { Start-Sleep -Milliseconds (500 * $intento) }
            continue
        }

        $content = (ConvertFrom-Json $respBody).choices[0].message.content
        if ($null -eq $content) { $content = "" }
        $limpio = $content.Trim()

        Write-Host "`n==> Respuesta cruda OpenAI: '$content'" -ForegroundColor Green

        if ($limpio -ieq "NO_BARCODE") {
            Write-Host "RESULTADO: NoBarcode (OpenAI no encontro codigo)." -ForegroundColor Yellow
            exit 0
        }

        $codigo = ($limpio -replace '[ \-\*]', '').ToUpperInvariant()
        if ($codigo -match '^([A-Z]+)(\d+)$') {
            Write-Host "RESULTADO: CodigoEncontrado -> $codigo  (Prefijo=$($Matches[1]) Numero=$($Matches[2]))" -ForegroundColor Green
            Write-Host "Archivo destino sugerido: $codigo.pdf"
        } else {
            Write-Host "RESULTADO: NoBarcode (texto no cumple patron [A-Z]+[0-9]+): '$codigo'" -ForegroundColor Yellow
        }
        exit 0
    }
    catch {
        Write-Host "Intento $intento excepcion: $($_.Exception.Message)" -ForegroundColor Red
        if ($intento -lt $MaxReintentos) { Start-Sleep -Milliseconds (500 * $intento) }
    }
    finally { $client.Dispose() }
}

Write-Host "==> OpenAI no respondio tras $MaxReintentos intentos." -ForegroundColor Red
exit 1
