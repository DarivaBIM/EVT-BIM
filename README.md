# EVT-BIM — Plugin Revit para Engenharia de Valor Tigre

Plugin Revit 2026 desenvolvido para uso interno da Tigre, no contexto do
programa Engenharia de Valor Tigre (EVT). Compartilha a base arquitetural
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

## Deploy

O target `DeployAddin` (em
`src/Plugins/DarivaBIM.Plugin.V2026/DarivaBIM.Plugin.V2026.csproj`)
executa o script `build/deploy_revit_2026.cmd` ao final do build e instala
o add-in em:

```
%ProgramData%\Autodesk\Revit\Addins\2026\EVT-BIM\
```

O manifest `EVT-BIM.V2026.addin` é copiado para
`%ProgramData%\Autodesk\Revit\Addins\2026\` e referencia o assembly
`DarivaBIM.Plugin.V2026.dll` na subpasta `EVT-BIM`. Após reabrir o Revit,
a aba "EVT-BIM" aparece na ribbon.
