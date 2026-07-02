@echo off
setlocal EnableExtensions

rem Conecta la unidad de red Informes como M:
rem Credenciales iguales a las de ArchivosScaneados (MasivosWorker).

set "RUTA_UNC=\\192.168.0.69\Informes"
set "LETRA=M:"
set "USUARIO=radicacion"
set "CLAVE=h3lph@rm@,+"

echo Conectando %LETRA% a %RUTA_UNC% ...

net use %LETRA% /delete /y >nul 2>&1
net use %LETRA% %RUTA_UNC% /user:%USUARIO% "%CLAVE%" /persistent:yes

if errorlevel 1 (
    echo.
    echo No se pudo conectar la unidad %LETRA%.
    echo Si aparece el error 1219, cierre otras conexiones a \\192.168.0.69 e intente de nuevo.
    exit /b 1
)

echo.
echo Unidad %LETRA% conectada correctamente a %RUTA_UNC%.
net use %LETRA%
exit /b 0
