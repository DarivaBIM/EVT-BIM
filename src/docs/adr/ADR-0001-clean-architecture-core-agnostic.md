# ADR-0001 — Clean Architecture com Core agnóstico

## Status
Aceito.

## Contexto
A versão monolítica do plugin (`FamiliesImporterHub.csproj`) misturava regras de
negócio (cálculos hidráulicos, regras Tigre/NBR 5626, montagem de grafo de
tubulação) com chamadas diretas à RevitAPI, WPF e HTTP. Isso impedia testar a
lógica fora do Revit, dificultava a migração para novas versões da API e
travava qualquer reuso em outros hosts (BIM360, Forge, CLI etc).

## Decisão
Adotamos **Clean Architecture** com a seguinte regra de dependência:

```
Domain ← Application ← (Revit.Abstractions | Infrastructure.*) ← Adapters.Vxxxx ← Plugin.Vxxxx
```

- `DarivaBIM.Domain`: entidades, value objects, regras puras. Não depende de
  RevitAPI, WPF, HTTP, banco de dados, UI ou filesystem.
- `DarivaBIM.Application`: casos de uso, contratos (interfaces) e DTOs. Não
  depende de RevitAPI.
- `DarivaBIM.Revit.Abstractions`: interfaces neutras (sem `Autodesk.Revit.*`)
  que descrevem o que o Core precisa do Revit.
- `DarivaBIM.Revit.Adapters.Vxxxx`: implementações concretas que **podem** usar
  RevitAPI da versão correspondente.
- `DarivaBIM.Plugin.Vxxxx`: ponto de entrada (`IExternalApplication`),
  `IExternalCommand`, ribbon e .addin.
- `DarivaBIM.Presentation.Wpf`: views/ViewModels que **não importam**
  `Autodesk.Revit.*`.
- `DarivaBIM.Infrastructure.*`: backend, licenciamento, persistência local,
  telemetria.

## Consequências
- Tipos como `Document`, `Element`, `ElementId`, `Connector`, `FamilyInstance`,
  `BuiltInParameter`, `Transaction`, `UIApplication`, `UIDocument` e
  `TaskDialog` **nunca** vazam para Domain ou Application.
- O Core pode ser testado com `FakeTigreCodeApplyService`,
  `InMemoryPipeRepository` etc.
- Adicionar uma nova versão do Revit (V2027 etc.) é uma operação local: novo
  Adapters.Vxxxx + novo Plugin.Vxxxx.
- Custo: existem ~22 projetos onde antes havia 1. Isso é absorvido por
  `Directory.Build.props` e por convenções de nome.
