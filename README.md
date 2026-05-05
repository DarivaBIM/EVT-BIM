# EVT-BIM — Plugin Revit para Engenharia de Valor Tigre

Plugin Revit 2025 / 2026 desenvolvido para uso interno da Tigre, no contexto
do programa Engenharia de Valor Tigre (EVT). Compartilha a base arquitetural
que servirá de origem para a versão 2 do produto comercial DarivaBIM.

## Stack

- Clean Architecture (camadas Domain, Application, Infrastructure,
  Presentation, Plugin) com Core agnóstico à RevitAPI.
- C# 12, .NET 8 (TFM `net8.0-windows`, `UseWPF=true`).
- Revit API 2026, com adapters multi-versão (V2023..V2027).
- WPF para a interface do usuário (dockable panes, janelas modais,
  páginas de feature).

## Build

Restaurar dependências e compilar o plugin do Revit 2026:

```
dotnet restore DarivaBIM.sln
dotnet build src\Plugins\DarivaBIM.Plugin.V2026\DarivaBIM.Plugin.V2026.csproj
```

Os nomes internos (`DarivaBIM.sln`, projetos `DarivaBIM.*`, namespaces
`DarivaBIM.*`, assembly `DarivaBIM.Plugin.V2026.dll`) são mantidos para
preservar a base como origem reaproveitável do DarivaBIM V2.

## Deploy local (dev)

O target `DeployAddin` nos csproj dos plugins executa o script
`build/deploy_revit_<ano>.cmd` ao final do build e instala o add-in em:

```
%ProgramData%\Autodesk\Revit\Addins\<ano>\EVT-BIM\
```

O manifest `EVT-BIM.V<ano>.addin` é copiado para
`%ProgramData%\Autodesk\Revit\Addins\<ano>\` e referencia o assembly
`DarivaBIM.Plugin.V<ano>.dll` na subpasta `EVT-BIM`. Após reabrir o Revit,
a aba "EVT-BIM" aparece na ribbon.

Para evitar o deploy local (ex.: em CI ou ao gerar instalador), rodar
`dotnet build` ou `dotnet publish` com `-p:SkipRevitDeploy=true`.

## Distribuição (instalador único)

O plugin é distribuído como `.exe` instalador único, gerado com Inno Setup.
Detecta automaticamente quais versões do Revit (2025/2026) estão presentes
e oferece modo **Recomendado** (todas detectadas) ou **Personalizado**.

**Build local apenas** (gera só o `.exe`, sem publicar):

```cmd
src\Build\Installers\build_installer.cmd
```

**Release pública no GitHub** (bumpa versão, builda, taga, faz push e
publica Release com o `.exe` anexado — tudo num comando):

```powershell
src\Build\Installers\release.ps1 -Version 0.1.1
```

Pré-requisitos da máquina dev:
[Inno Setup 6](https://jrsoftware.org/isinfo.php) (compilador) e — para o
`release.ps1` — [GitHub CLI](https://cli.github.com/) autenticado.
Documentação completa em
[`src/Build/Installers/README.md`](src/Build/Installers/README.md). A
arquitetura do pipeline e o roadmap para CI hospedado estão no
[ADR-0018](src/docs/adr/ADR-0018-release-pipeline.md).
