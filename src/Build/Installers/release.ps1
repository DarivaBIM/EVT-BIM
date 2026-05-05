<#
.SYNOPSIS
  Lanca uma nova versao do plugin EVT-BIM: bump de versao, build do
  instalador e publicacao de Release no GitHub com o .exe anexado.

.DESCRIPTION
  Pipeline de release local (Fase 1 do ADR-0018). Substitui um workflow
  GitHub Actions enquanto o build depende de RevitAPI.dll local.

  Ordem de operacoes (intencional - o tag so existe se o build passou):
    1. Valida pre-requisitos (gh autenticado, branch, working tree limpo).
    2. Atualiza versao em Directory.Build.props e EVT-BIM.iss.
    3. Roda build_installer.cmd e valida o .exe gerado.
    4. Commit + tag + push.
    5. gh release create com o .exe anexado.

  Se o build falhar, a versao fica bumpada localmente mas SEM commit ou tag
  - basta `git restore` nos dois arquivos e ajustar o que quebrou.

.PARAMETER Version
  Nova versao em SemVer simples (X.Y.Z). Ex: 0.1.1, 1.0.0.

.PARAMETER Notes
  Corpo do release (texto literal). Se omitido, usa --generate-notes
  (GitHub gera a partir dos commits desde o ultimo tag).

.PARAMETER NotesFile
  Caminho para arquivo .md com o corpo do release. Tem prioridade sobre -Notes.

.PARAMETER Prerelease
  Marca o release como pre-release no GitHub (nao aparece como "Latest").

.PARAMETER DryRun
  Faz tudo ate o build, mas nao commita, nao taga, nao publica.
  Usado para validar localmente que o pipeline esta funcionando.

.PARAMETER AllowDirty
  Permite rodar com working tree sujo. Use so se souber o que esta fazendo -
  os arquivos nao-commitados nao entram no .exe, podem mascarar bugs.

.EXAMPLE
  .\release.ps1 -Version 0.1.1
  Release patch comum.

.EXAMPLE
  .\release.ps1 -Version 0.2.0 -NotesFile .\notes-0.2.0.md
  Release com notas escritas a mao.

.EXAMPLE
  .\release.ps1 -Version 0.2.0-rc1 -Prerelease -DryRun
  Ensaio de release sem publicar nada.
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version,

    [string]$Notes,
    [string]$NotesFile,
    [switch]$Prerelease,
    [switch]$DryRun,
    [switch]$AllowDirty
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ---------------------------------------------------------------------------
# Paths
# ---------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = (Resolve-Path (Join-Path $ScriptDir '..\..\..')).Path
$PropsFile = Join-Path $RepoRoot 'src\Build\Directory.Build.props'
$IssFile   = Join-Path $ScriptDir 'EVT-BIM.iss'
$BuildCmd  = Join-Path $ScriptDir 'build_installer.cmd'
$Tag       = "v$Version"
$ExePath   = Join-Path $RepoRoot "artifacts\installer\EVT-BIM-Setup-$Tag.exe"

function Step([string]$Msg) { Write-Host "==> $Msg" -ForegroundColor Cyan }
function Ok  ([string]$Msg) { Write-Host "    $Msg" -ForegroundColor DarkGray }
function Die ([string]$Msg) { Write-Host "[ERRO] $Msg" -ForegroundColor Red; exit 1 }

# ---------------------------------------------------------------------------
# 1. Pre-requisitos
# ---------------------------------------------------------------------------
Step "Validando pre-requisitos"

# Em dry-run, gh nao e usado — vira warning. Em release real, e bloqueante.
$ghMissing = -not (Get-Command gh -ErrorAction SilentlyContinue)
if ($ghMissing) {
    if ($DryRun) {
        Write-Host "[AVISO] gh nao encontrado. Em release real seria bloqueante: https://cli.github.com/" -ForegroundColor Yellow
    } else {
        Die "GitHub CLI (gh) nao encontrado no PATH. Instale: https://cli.github.com/"
    }
} else {
    gh auth status 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        if ($DryRun) {
            Write-Host "[AVISO] gh nao autenticado. Em release real seria bloqueante. Rode: gh auth login" -ForegroundColor Yellow
        } else {
            Die "gh nao esta autenticado. Rode: gh auth login"
        }
    } else {
        Ok "gh autenticado"
    }
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Die ".NET SDK (dotnet) nao encontrado no PATH."
}
Ok "dotnet OK"

# ISCC.exe e validado pelo proprio build_installer.cmd; nao duplicar aqui.

# ---------------------------------------------------------------------------
# 2. Estado do repositorio
# ---------------------------------------------------------------------------
Step "Validando estado do repositorio"
Push-Location $RepoRoot
try {
    $branch = (git rev-parse --abbrev-ref HEAD).Trim()
    if ($branch -ne 'main') {
        if (-not $AllowDirty) {
            Die "Branch atual e '$branch', nao 'main'. Use -AllowDirty para forcar."
        }
        Write-Host "[AVISO] Releasing de branch '$branch' (nao main)" -ForegroundColor Yellow
    } else {
        Ok "branch=main"
    }

    $status = git status --porcelain
    if ($status -and -not $AllowDirty) {
        Die "Working tree sujo. Commite/stashe ou use -AllowDirty.`n$status"
    }
    if (-not $status) { Ok "working tree limpo" }

    $existingTag = git tag --list $Tag
    if ($existingTag) { Die "Tag $Tag ja existe localmente. Escolha outra versao ou apague: git tag -d $Tag" }

    git ls-remote --tags origin $Tag 2>$null | Out-Null
    $remoteTag = git ls-remote --tags origin $Tag
    if ($remoteTag) { Die "Tag $Tag ja existe no remote. Escolha outra versao." }
    Ok "tag $Tag livre"
} finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# 3. Atualiza versao nos dois arquivos
# ---------------------------------------------------------------------------
Step "Atualizando versao para $Version"

# Directory.Build.props: <Version>, <AssemblyVersion>, <FileVersion>
$asmVer = "$Version.0"
$props = Get-Content $PropsFile -Raw
$props = [regex]::Replace($props, '<Version>[^<]+</Version>',                 "<Version>$Version</Version>")
$props = [regex]::Replace($props, '<AssemblyVersion>[^<]+</AssemblyVersion>', "<AssemblyVersion>$asmVer</AssemblyVersion>")
$props = [regex]::Replace($props, '<FileVersion>[^<]+</FileVersion>',         "<FileVersion>$asmVer</FileVersion>")
Set-Content -Path $PropsFile -Value $props -NoNewline -Encoding utf8
Ok "Directory.Build.props -> $Version / $asmVer"

# EVT-BIM.iss: #define AppVersion "X.Y.Z"
$iss = Get-Content $IssFile -Raw
$iss = [regex]::Replace($iss, '#define\s+AppVersion\s+"[^"]+"', "#define AppVersion     `"$Version`"")
Set-Content -Path $IssFile -Value $iss -NoNewline -Encoding utf8
Ok "EVT-BIM.iss     -> $Version"

# ---------------------------------------------------------------------------
# 4. Build do instalador
# ---------------------------------------------------------------------------
Step "Rodando build_installer.cmd (pode demorar)"
& cmd /c "`"$BuildCmd`""
if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "[ERRO] build_installer.cmd falhou (exit $LASTEXITCODE)." -ForegroundColor Red
    Write-Host "       Os bumps de versao NAO foram commitados." -ForegroundColor Red
    Write-Host "       Para descartar e tentar de novo:" -ForegroundColor Red
    Write-Host "         git restore src/Build/Directory.Build.props src/Build/Installers/EVT-BIM.iss" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $ExePath)) {
    Die "Build terminou OK, mas nao encontrei o .exe esperado: $ExePath"
}
$exeSize = [math]::Round((Get-Item $ExePath).Length / 1MB, 1)
Ok ".exe gerado: $ExePath ($exeSize MB)"

# ---------------------------------------------------------------------------
# 5. DryRun para aqui
# ---------------------------------------------------------------------------
if ($DryRun) {
    Write-Host ""
    Write-Host "[DRY RUN] Tudo OK ate aqui. Nao foi feito commit, tag, push ou release." -ForegroundColor Yellow
    Write-Host "          Para descartar os bumps de versao:" -ForegroundColor Yellow
    Write-Host "            git restore src/Build/Directory.Build.props src/Build/Installers/EVT-BIM.iss" -ForegroundColor Yellow
    exit 0
}

# ---------------------------------------------------------------------------
# 6. Commit + tag + push
# ---------------------------------------------------------------------------
Step "Commit + tag + push"
Push-Location $RepoRoot
try {
    git add src/Build/Directory.Build.props src/Build/Installers/EVT-BIM.iss
    if ($LASTEXITCODE -ne 0) { Die "git add falhou" }

    git commit -m "release: $Tag"
    if ($LASTEXITCODE -ne 0) { Die "git commit falhou (sem mudancas pendentes?)" }
    Ok "commit criado"

    git tag -a $Tag -m "EVT-BIM $Tag"
    if ($LASTEXITCODE -ne 0) { Die "git tag falhou" }
    Ok "tag $Tag criado"

    git push origin HEAD
    if ($LASTEXITCODE -ne 0) { Die "git push (commit) falhou" }

    git push origin $Tag
    if ($LASTEXITCODE -ne 0) { Die "git push (tag) falhou. O release nao foi criado - rode novamente apos resolver, ou crie manualmente: gh release create $Tag `"$ExePath`"" }
    Ok "push concluido"
} finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# 7. GitHub Release
# ---------------------------------------------------------------------------
Step "Criando GitHub Release"
$ghArgs = @('release', 'create', $Tag, $ExePath, '--title', "EVT-BIM $Tag")
if ($Prerelease) { $ghArgs += '--prerelease' }
if ($NotesFile) {
    if (-not (Test-Path $NotesFile)) { Die "NotesFile nao existe: $NotesFile" }
    $ghArgs += @('--notes-file', (Resolve-Path $NotesFile).Path)
} elseif ($Notes) {
    $ghArgs += @('--notes', $Notes)
} else {
    $ghArgs += '--generate-notes'
}

& gh @ghArgs
if ($LASTEXITCODE -ne 0) {
    Die "gh release create falhou. A tag ja foi pushed. Tente manualmente:`n  gh release create $Tag `"$ExePath`" --generate-notes"
}

Write-Host ""
Write-Host "===== Release $Tag publicado =====" -ForegroundColor Green
& gh release view $Tag --web 2>$null
