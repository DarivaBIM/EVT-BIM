@echo off
setlocal

set "PROJECT_DIR=%~1"
set "TARGET_DIR=%~2"

if "%PROJECT_DIR%"=="" (
    echo ERRO: PROJECT_DIR nao foi informado.
    exit /b 1
)

if "%TARGET_DIR%"=="" (
    echo ERRO: TARGET_DIR nao foi informado.
    exit /b 1
)

REM Aborta cedo se o Revit estiver aberto: as DLLs do add-in ficam travadas
REM e o copy falha no meio do build com mensagem criptica.
tasklist /FI "IMAGENAME eq Revit.exe" 2>NUL | find /I "Revit.exe" >NUL
if not errorlevel 1 (
    echo ERRO: Revit.exe esta em execucao. Feche o Revit antes de buildar/deploy.
    exit /b 1
)

set "ADDIN_NAME=EVT-BIM"
set "ASSEMBLY_NAME=DarivaBIM.Plugin.V2026"
set "ADDIN_FILE=EVT-BIM.V2026.addin"
set "ADDIN_SOURCE=%PROJECT_DIR%..\..\Build\AddinManifests\%ADDIN_FILE%"

set "LEGACY_ADDIN_NAME=DarivaBIM"
set "LEGACY_ADDIN_FILE=DarivaBIM.V2026.addin"

set "RVT_ADDIN_PATH=%ProgramData%\Autodesk\Revit\Addins\2026"
set "ADDIN_SUBFOLDER=%RVT_ADDIN_PATH%\%ADDIN_NAME%"
set "USER_ADDIN_PATH=%AppData%\Autodesk\Revit\Addins\2026"

echo ===== Deploy %ADDIN_NAME% =====
echo PROJECT_DIR   = "%PROJECT_DIR%"
echo TARGET_DIR    = "%TARGET_DIR%"
echo ADDIN_SOURCE  = "%ADDIN_SOURCE%"
echo PROGRAMDATA   = "%RVT_ADDIN_PATH%"
echo SUBFOLDER     = "%ADDIN_SUBFOLDER%"

if not exist "%RVT_ADDIN_PATH%" mkdir "%RVT_ADDIN_PATH%"
if errorlevel 1 (
    echo ERRO: nao foi possivel criar "%RVT_ADDIN_PATH%"
    exit /b 1
)

if not exist "%ADDIN_SUBFOLDER%" mkdir "%ADDIN_SUBFOLDER%"
if errorlevel 1 (
    echo ERRO: nao foi possivel criar "%ADDIN_SUBFOLDER%"
    exit /b 1
)

REM Remove legacy AppData artifacts from older builds.
if exist "%USER_ADDIN_PATH%\FamiliesImporterHub.addin" del /F /Q "%USER_ADDIN_PATH%\FamiliesImporterHub.addin"
if exist "%USER_ADDIN_PATH%\FamiliesImporterHub" rmdir /S /Q "%USER_ADDIN_PATH%\FamiliesImporterHub"
if exist "%USER_ADDIN_PATH%\%ADDIN_FILE%" del /F /Q "%USER_ADDIN_PATH%\%ADDIN_FILE%"
if exist "%USER_ADDIN_PATH%\%ADDIN_NAME%" rmdir /S /Q "%USER_ADDIN_PATH%\%ADDIN_NAME%"

REM Remove legacy TigreBIM-era artifacts (when this add-in was named DarivaBIM).
if exist "%RVT_ADDIN_PATH%\%LEGACY_ADDIN_FILE%" del /F /Q "%RVT_ADDIN_PATH%\%LEGACY_ADDIN_FILE%"
if exist "%RVT_ADDIN_PATH%\%LEGACY_ADDIN_NAME%" rmdir /S /Q "%RVT_ADDIN_PATH%\%LEGACY_ADDIN_NAME%"

copy /Y "%ADDIN_SOURCE%" "%RVT_ADDIN_PATH%\%ADDIN_FILE%"
if errorlevel 1 (
    echo ERRO: falha ao copiar %ADDIN_FILE%
    exit /b 1
)

copy /Y "%TARGET_DIR%%ASSEMBLY_NAME%.dll" "%ADDIN_SUBFOLDER%\%ASSEMBLY_NAME%.dll"
if errorlevel 1 (
    echo ERRO: falha ao copiar %ASSEMBLY_NAME%.dll
    exit /b 1
)

if exist "%TARGET_DIR%%ASSEMBLY_NAME%.pdb" (
    copy /Y "%TARGET_DIR%%ASSEMBLY_NAME%.pdb" "%ADDIN_SUBFOLDER%\%ASSEMBLY_NAME%.pdb"
)

if exist "%TARGET_DIR%%ASSEMBLY_NAME%.deps.json" (
    copy /Y "%TARGET_DIR%%ASSEMBLY_NAME%.deps.json" "%ADDIN_SUBFOLDER%\%ASSEMBLY_NAME%.deps.json"
)

if exist "%TARGET_DIR%%ASSEMBLY_NAME%.runtimeconfig.json" (
    copy /Y "%TARGET_DIR%%ASSEMBLY_NAME%.runtimeconfig.json" "%ADDIN_SUBFOLDER%\%ASSEMBLY_NAME%.runtimeconfig.json"
)

REM Copy referenced project DLLs and resources next to the plugin assembly.
REM Sem redirecionar para nul: erros precisam aparecer no log de build.
xcopy /Y /D /I "%TARGET_DIR%DarivaBIM.*.dll" "%ADDIN_SUBFOLDER%\"
if errorlevel 1 (
    echo ERRO: falha ao copiar DLLs de dependencia.
    exit /b 1
)

if exist "%TARGET_DIR%Resources" (
    xcopy /Y /D /I "%TARGET_DIR%Resources" "%ADDIN_SUBFOLDER%\Resources\"
    if errorlevel 1 (
        echo ERRO: falha ao copiar Resources.
        exit /b 1
    )
)

if exist "%TARGET_DIR%Ribbon" (
    xcopy /Y /D /I /E "%TARGET_DIR%Ribbon" "%ADDIN_SUBFOLDER%\Ribbon\"
    if errorlevel 1 (
        echo ERRO: falha ao copiar Ribbon.
        exit /b 1
    )
)

REM Sidecar EXE (DarivaBIM.FamilyBrowser) e suas dependencias.
REM Vive em subpasta dedicada pra evitar misturar com as DLLs do plugin
REM (o sidecar carrega proprias copias de WebView2.*.dll, System.Text.Json
REM etc., e roda em processo separado).
if exist "%TARGET_DIR%Sidecar" (
    xcopy /Y /D /I /E "%TARGET_DIR%Sidecar" "%ADDIN_SUBFOLDER%\Sidecar\"
    if errorlevel 1 (
        echo ERRO: falha ao copiar Sidecar.
        exit /b 1
    )
)

echo Deploy concluido com sucesso.
exit /b 0
