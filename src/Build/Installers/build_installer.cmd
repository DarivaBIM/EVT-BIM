@echo off
setlocal enabledelayedexpansion

REM ============================================================================
REM  build_installer.cmd
REM
REM  Empacota o plugin EVT-BIM em um instalador unico (.exe) usando Inno Setup.
REM
REM  Etapas:
REM    1. dotnet publish -c Release dos plugins V2025 e V2026
REM    2. organiza os artefatos em artifacts/installer/v{2025,2026}/{addin,plugin}
REM    3. invoca ISCC.exe sobre EVT-BIM.iss
REM
REM  Saida final: artifacts/installer/EVT-BIM-Setup-vX.Y.Z.exe
REM ============================================================================

REM ---- Localiza pastas relativas a este script ------------------------------
set "SCRIPT_DIR=%~dp0"
set "REPO_ROOT=%SCRIPT_DIR%..\..\.."
pushd "%REPO_ROOT%" >nul
set "REPO_ROOT=%CD%"
popd >nul

set "STAGE=%REPO_ROOT%\artifacts\installer"
set "PLUGIN_V2025=%REPO_ROOT%\src\Plugins\DarivaBIM.Plugin.V2025\DarivaBIM.Plugin.V2025.csproj"
set "PLUGIN_V2026=%REPO_ROOT%\src\Plugins\DarivaBIM.Plugin.V2026\DarivaBIM.Plugin.V2026.csproj"
set "ISS_FILE=%SCRIPT_DIR%EVT-BIM.iss"

REM ---- Localiza o ISCC ------------------------------------------------------
REM Prioriza ISCC no PATH; se nao houver, tenta o caminho padrao do Inno 6.
set "ISCC="
for %%I in (ISCC.exe) do set "ISCC=%%~$PATH:I"
if "%ISCC%"=="" if exist "%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe" set "ISCC=%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if "%ISCC%"=="" if exist "%ProgramFiles%\Inno Setup 6\ISCC.exe"      set "ISCC=%ProgramFiles%\Inno Setup 6\ISCC.exe"

if "%ISCC%"=="" (
    echo [ERRO] Inno Setup 6 nao encontrado.
    echo        Instale via https://jrsoftware.org/isinfo.php
    echo        ou adicione ISCC.exe ao PATH.
    exit /b 1
)

echo ===== EVT-BIM ^| build_installer =====
echo REPO_ROOT  = %REPO_ROOT%
echo STAGE      = %STAGE%
echo ISCC       = %ISCC%
echo.

REM ---- Limpa staging --------------------------------------------------------
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
mkdir "%STAGE%\v2025\addin"  || goto :fail
mkdir "%STAGE%\v2025\plugin" || goto :fail
mkdir "%STAGE%\v2026\addin"  || goto :fail
mkdir "%STAGE%\v2026\plugin" || goto :fail

REM ---- Publish dos plugins --------------------------------------------------
REM SkipRevitDeploy=true desativa o target DeployAddin nos csproj, evitando
REM que o publish para o instalador tambem reinstale o plugin localmente
REM (esse fluxo e exclusivo do dev, nao tem sentido durante empacotamento).

echo [1/3] Publicando DarivaBIM.Plugin.V2025...
dotnet publish "%PLUGIN_V2025%" -c Release -o "%STAGE%\v2025\plugin" -p:SkipRevitDeploy=true || goto :fail

echo.
echo [2/3] Publicando DarivaBIM.Plugin.V2026...
dotnet publish "%PLUGIN_V2026%" -c Release -o "%STAGE%\v2026\plugin" -p:SkipRevitDeploy=true || goto :fail

REM ---- Move os .addin para a sub-pasta separada -----------------------------
REM O .iss espera o manifesto em v{ano}\addin\ e os binarios em v{ano}\plugin\
REM (DestDir distintos). Mover evita que o glob de plugin\* pegue o manifesto.

if exist "%STAGE%\v2025\plugin\EVT-BIM.V2025.addin" (
    move /Y "%STAGE%\v2025\plugin\EVT-BIM.V2025.addin" "%STAGE%\v2025\addin\" >nul || goto :fail
) else (
    echo [ERRO] Manifesto EVT-BIM.V2025.addin nao foi gerado pelo publish.
    goto :fail
)

if exist "%STAGE%\v2026\plugin\EVT-BIM.V2026.addin" (
    move /Y "%STAGE%\v2026\plugin\EVT-BIM.V2026.addin" "%STAGE%\v2026\addin\" >nul || goto :fail
) else (
    echo [ERRO] Manifesto EVT-BIM.V2026.addin nao foi gerado pelo publish.
    goto :fail
)

REM ---- Compila o instalador -------------------------------------------------
echo.
echo [3/3] Compilando instalador via Inno Setup...
"%ISCC%" "%ISS_FILE%" || goto :fail

echo.
echo ===== Instalador gerado com sucesso =====
for %%F in ("%STAGE%\EVT-BIM-Setup-*.exe") do echo   %%~fF
exit /b 0

:fail
echo.
echo ===== FALHA NA GERACAO DO INSTALADOR =====
exit /b 1
