# Installers

Pipeline de empacotamento do plugin EVT-BIM em um instalador único (`.exe`)
distribuível para o usuário final, gerado com [Inno Setup](https://jrsoftware.org/isinfo.php).

## Conteúdo

- `EVT-BIM.iss` — script Inno Setup que define o instalador (componentes,
  destinos, lógica de detecção do Revit, modos Recomendado/Personalizado).
- `build_installer.cmd` — script de orquestração que faz `dotnet publish` dos
  plugins V2025 e V2026, organiza o staging em `artifacts/installer/` e chama
  o `ISCC.exe` para gerar o `.exe` final.
- `release.ps1` — pipeline de release (Fase 1, ADR-0018): bumpa versão,
  roda o build, taga, faz push e publica GitHub Release com o `.exe`.

## Pré-requisitos (uma vez por máquina dev)

1. .NET 8 SDK.
2. Revit 2025 e/ou 2026 instalados (apenas para o build dos adapters; o
   instalador final não precisa de Revit na máquina onde o `.exe` for usado
   além de detectá-lo).
3. **[Inno Setup 6](https://jrsoftware.org/isinfo.php)** — instalar o
   compilador. O `build_installer.cmd` procura `ISCC.exe` no `PATH` e nas
   pastas padrão `C:\Program Files (x86)\Inno Setup 6\` e
   `C:\Program Files\Inno Setup 6\`.
4. **[GitHub CLI (`gh`)](https://cli.github.com/)** autenticado (`gh auth
   login`) — só necessário para publicar releases via `release.ps1`.
   Se você só quiser gerar o `.exe` localmente, pode pular.

## Como gerar o instalador (build local apenas)

Da raiz do repositório (ou de qualquer pasta — o script resolve caminhos
relativos a si mesmo):

```cmd
src\Build\Installers\build_installer.cmd
```

Saída: `artifacts\installer\EVT-BIM-Setup-v<versão>.exe` (a versão vem do
`#define AppVersion` no `.iss`).

Esse `.exe` é o que vai para os usuários — basta enviar o arquivo (e-mail,
SharePoint, link de download, etc.).

## Como publicar uma release no GitHub

Use `release.ps1` no lugar de chamar `build_installer.cmd` direto. Ele
bumpa a versão nos dois arquivos onde ela vive (`Directory.Build.props` e
`EVT-BIM.iss`), builda o instalador, taga, faz push e cria um Release no
GitHub com o `.exe` anexado — tudo num comando.

```powershell
# Release patch comum (notas auto-geradas dos commits)
src\Build\Installers\release.ps1 -Version 0.1.1

# Com notas escritas à mão
src\Build\Installers\release.ps1 -Version 0.2.0 -NotesFile .\notes-0.2.0.md

# Ensaio sem publicar nada (valida que o pipeline está OK)
src\Build\Installers\release.ps1 -Version 0.2.0 -DryRun
```

Pré-requisitos: `gh auth login` feito, branch `main`, working tree limpo.
Veja `.\release.ps1 -?` para o help completo, e o **ADR-0018** para a
decisão de arquitetura por trás desse fluxo (e os gatilhos para migrar
para CI hospedado no futuro).

Se o build falhar, os bumps de versão ficam **não-commitados** — descarte
com:

```powershell
git restore src/Build/Directory.Build.props src/Build/Installers/EVT-BIM.iss
```

## Como o usuário final instala

1. Executar `EVT-BIM-Setup-v0.1.0.exe` (precisa aprovar UAC — escreve em
   `%ProgramData%`).
2. Escolher modo:
   - **Recomendado**: marca automaticamente apenas as versões do Revit
     detectadas na máquina (por pasta padrão ou registro).
   - **Personalizado**: usuário marca livremente Revit 2025 e/ou 2026,
     mesmo versões ainda não instaladas (útil para preparar máquinas).
3. Reabrir o Revit. A aba "EVT-BIM" aparece na ribbon.

Para desinstalar: "Adicionar ou remover programas" do Windows → "EVT-BIM" →
Desinstalar (remove `.addin` e a subpasta `EVT-BIM\`).

## Onde os arquivos vão parar

```
%ProgramData%\Autodesk\Revit\Addins\2025\
├── EVT-BIM.V2025.addin
└── EVT-BIM\
    ├── DarivaBIM.Plugin.V2025.dll
    ├── DarivaBIM.*.dll        (dependências do Core/Application/etc.)
    ├── Resources\             (tigre_codes.json, etc.)
    └── Ribbon\                (ícones)

%ProgramData%\Autodesk\Revit\Addins\2026\
└── (estrutura simétrica para V2026)
```

Os manifestos (`.addin`) usam caminho **relativo** ao próprio manifesto, então
funcionam em qualquer drive onde o Windows estiver instalado (não dependem de
`C:` fixo).

## Versionamento

A versão vive em **dois lugares** que precisam ficar sincronizados:

- `src/Build/Directory.Build.props` — `<Version>`, `<AssemblyVersion>`,
  `<FileVersion>` (entra no metadata das DLLs).
- `src/Build/Installers/EVT-BIM.iss` — `#define AppVersion` (entra no
  nome do `.exe` e em "Adicionar ou remover programas").

O `release.ps1` cuida dos dois automaticamente. Se for editar à mão (raro),
não esqueça do segundo.

O `AppId` no `.iss` é fixo (GUID estável) — isso garante que reinstalações
desinstalem corretamente a versão anterior. **Não alterar** sem migrar.
