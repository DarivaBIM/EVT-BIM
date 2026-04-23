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

set "RVT_ADDIN_PATH=%ProgramData%\Autodesk\Revit\Addins\2026"
set "ADDIN_SUBFOLDER=%RVT_ADDIN_PATH%\FamiliesImporterHub"
set "USER_ADDIN_PATH=%AppData%\Autodesk\Revit\Addins\2026"

echo ===== Deploy FamiliesImporterHub =====
echo PROJECT_DIR   = "%PROJECT_DIR%"
echo TARGET_DIR    = "%TARGET_DIR%"
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

if exist "%USER_ADDIN_PATH%\FamiliesImporterHub.addin" (
    del /F /Q "%USER_ADDIN_PATH%\FamiliesImporterHub.addin"
    if errorlevel 1 (
        echo ERRO: nao foi possivel remover manifesto antigo do AppData.
        exit /b 1
    )
)

if exist "%USER_ADDIN_PATH%\FamiliesImporterHub" (
    rmdir /S /Q "%USER_ADDIN_PATH%\FamiliesImporterHub"
)

copy /Y "%PROJECT_DIR%FamiliesImporterHub.addin" "%RVT_ADDIN_PATH%\FamiliesImporterHub.addin"
if errorlevel 1 (
    echo ERRO: falha ao copiar FamiliesImporterHub.addin
    exit /b 1
)

copy /Y "%TARGET_DIR%FamiliesImporterHub.dll" "%ADDIN_SUBFOLDER%\FamiliesImporterHub.dll"
if errorlevel 1 (
    echo ERRO: falha ao copiar FamiliesImporterHub.dll
    exit /b 1
)

if exist "%TARGET_DIR%FamiliesImporterHub.pdb" (
    copy /Y "%TARGET_DIR%FamiliesImporterHub.pdb" "%ADDIN_SUBFOLDER%\FamiliesImporterHub.pdb"
    if errorlevel 1 (
        echo ERRO: falha ao copiar FamiliesImporterHub.pdb
        exit /b 1
    )
)

if exist "%TARGET_DIR%FamiliesImporterHub.deps.json" (
    copy /Y "%TARGET_DIR%FamiliesImporterHub.deps.json" "%ADDIN_SUBFOLDER%\FamiliesImporterHub.deps.json"
    if errorlevel 1 (
        echo ERRO: falha ao copiar FamiliesImporterHub.deps.json
        exit /b 1
    )
)

if exist "%TARGET_DIR%FamiliesImporterHub.runtimeconfig.json" (
    copy /Y "%TARGET_DIR%FamiliesImporterHub.runtimeconfig.json" "%ADDIN_SUBFOLDER%\FamiliesImporterHub.runtimeconfig.json"
    if errorlevel 1 (
        echo ERRO: falha ao copiar FamiliesImporterHub.runtimeconfig.json
        exit /b 1
    )
)

echo Deploy concluido com sucesso.
exit /b 0