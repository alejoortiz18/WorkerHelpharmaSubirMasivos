@echo off
setlocal EnableExtensions

call :EnsureAdmin
if errorlevel 1 exit /b 1

set "INSTALL_DIR=%~dp0"
if "%INSTALL_DIR:~-1%"=="\" set "INSTALL_DIR=%INSTALL_DIR:~0,-1%"

set "EXE=%INSTALL_DIR%\MasivosWorker.exe"
set "SERVICE_NAME=MasivosWorker"
set "DISPLAY_NAME=Helpharma Masivos Worker (Worker 2)"
set "DESCRIPTION=Worker 2: procesa lotes TXT de la NAS UNC, barcode, APIs y OpenAI."

if not exist "%EXE%" (
    echo No se encontro "%EXE%".
    echo Publique el proyecto antes de instalar el servicio.
    exit /b 1
)

powershell -NoProfile -ExecutionPolicy Bypass -Command "if (-not [System.Diagnostics.EventLog]::SourceExists('%SERVICE_NAME%')) { New-EventLog -LogName Application -Source '%SERVICE_NAME%' }" >nul 2>&1

sc query "%SERVICE_NAME%" >nul 2>&1
if %errorlevel% equ 0 (
    echo Servicio existente detectado. Deteniendo...
    sc stop "%SERVICE_NAME%" >nul 2>&1
    timeout /t 2 /nobreak >nul

    echo Actualizando servicio %SERVICE_NAME%...
    sc config "%SERVICE_NAME%" binPath= "\"%EXE%\"" start= auto >nul || goto :Fail
    sc description "%SERVICE_NAME%" "%DESCRIPTION%" >nul || goto :Fail
) else (
    echo Creando servicio %SERVICE_NAME%...
    sc create "%SERVICE_NAME%" binPath= "\"%EXE%\"" start= auto DisplayName= "%DISPLAY_NAME%" >nul || goto :Fail
    sc description "%SERVICE_NAME%" "%DESCRIPTION%" >nul || goto :Fail
)

reg add "HKLM\SYSTEM\CurrentControlSet\Services\%SERVICE_NAME%\Environment" /f >nul || goto :Fail
reg add "HKLM\SYSTEM\CurrentControlSet\Services\%SERVICE_NAME%\Environment" /v DOTNET_ENVIRONMENT /t REG_SZ /d Production /f >nul || goto :Fail
reg add "HKLM\SYSTEM\CurrentControlSet\Services\%SERVICE_NAME%\Environment" /v ASPNETCORE_ENVIRONMENT /t REG_SZ /d Production /f >nul || goto :Fail

echo Iniciando servicio...
sc start "%SERVICE_NAME%" >nul || goto :Fail
timeout /t 3 /nobreak >nul

for /f "tokens=3 delims=: " %%A in ('sc query "%SERVICE_NAME%" ^| findstr /R /C:"STATE"') do set "SERVICE_STATE=%%A"
echo Estado: %SERVICE_STATE%
echo.
echo Logs: Visor de eventos ^> Registros de Windows ^> Aplicacion ^> origen %SERVICE_NAME%
echo Config: %INSTALL_DIR%\appsettings.json
exit /b 0

:Fail
echo.
echo La instalacion del servicio fallo.
echo Valide que la consola este en modo administrador y que el .exe exista en esta carpeta.
exit /b 1

:EnsureAdmin
powershell -NoProfile -ExecutionPolicy Bypass -Command "$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent()); if ($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { exit 0 } else { exit 1 }" >nul 2>&1
if %errorlevel% equ 0 exit /b 0

echo Solicitando permisos de administrador...
powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%ComSpec%' -ArgumentList '/c ""%~f0""' -Verb RunAs" >nul 2>&1
exit /b 1
