# Installers

Pipeline de empacotamento do plugin EVT-BIM em um instalador único (`.exe`)
distribuível para o usuário final, gerado com [Inno Setup](https://jrsoftware.org/isinfo.php).

## Conteúdo

- `EVT-BIM.iss` — script Inno Setup que define o instalador (componentes,
  destinos, lógica de detecção do Revit, modos Recomendado/Personalizado).
- `build_installer.cmd` — script de orquestração que faz `dotnet publish` dos
  plugins V2025 e V2026, organiza o staging em `artifacts/installer/` e chama
  o `ISCC.exe` para gerar o `.exe` final.

## Pré-requisitos (uma vez por máquina dev)

1. .NET 8 SDK.
2. Revit 2025 e/ou 2026 instalados (apenas para o build dos adapters; o
   instalador final não precisa de Revit na máquina onde o `.exe` for usado
   além de detectá-lo).
3. **[Inno Setup 6](https://jrsoftware.org/isinfo.php)** — instalar o
   compilador. O `build_installer.cmd` procura `ISCC.exe` no `PATH` e nas
   pastas padrão `C:\Program Files (x86)\Inno Setup 6\` e
   `C:\Program Files\Inno Setup 6\`.

## Como gerar o instalador

Da raiz do repositório (ou de qualquer pasta — o script resolve caminhos
relativos a si mesmo):

```cmd
src\Build\Installers\build_installer.cmd
```

Saída: `artifacts\installer\EVT-BIM-Setup-v0.1.0.exe`.

Esse `.exe` é o que vai para os usuários — basta enviar o arquivo (e-mail,
SharePoint, link de download, etc.).

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

A versão exibida no instalador (`EVT-BIM-Setup-v0.1.0.exe` e em
"Adicionar ou remover programas") está hardcoded em
`EVT-BIM.iss` na diretiva `#define AppVersion`. Atualizar junto com a
`<Version>` em `src/Build/Directory.Build.props` quando subir versão.

O `AppId` no `.iss` é fixo (GUID estável) — isso garante que reinstalações
desinstalem corretamente a versão anterior. **Não alterar** sem migrar.
