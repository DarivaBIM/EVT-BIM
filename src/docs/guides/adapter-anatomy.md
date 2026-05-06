# Anatomia do Revit Adapter

A camada `DarivaBIM.Revit.Adapters.V2026` é onde mora **o código real que
mexe no Revit**. Ela está separada em duas grandes pastas: `Common/` (blocos
reutilizáveis) e `Features/` (implementação específica de cada ferramenta).

## Common — blocos reutilizáveis de RevitAPI

Use `Common/` para tudo que faria sentido em mais de uma feature. Subpastas:

| Pasta              | Para o quê                                                          |
|--------------------|----------------------------------------------------------------------|
| `Cad/`             | Geometria de vínculos CAD, segmentação de polylines/arcs.            |
| `Elements/`        | Helpers genéricos sobre `Element`/`ElementId`.                       |
| `Filters/`         | `ISelectionFilter` reutilizáveis (ex.: `CadCurveSelectionFilter`).   |
| `Parameters/`      | Leitura/escrita de `Parameter` (`ParameterTextReader`, `RevitParameterReader`, `RevitParameterWriter`). |
| `Pipes/`           | Conector e tubo: ligação entre extremidades, busca por proximidade. |
| `Selection/`       | Helpers genéricos de seleção (compatíveis entre versões).            |
| `SharedParameters/`| Definição/criação/binding de shared parameters (genérico).           |
| `Transactions/`    | `RevitTransactionRunner`, preprocessors de falhas em `FailurePreprocessors/`. |
| `Units/`           | Conversões (`RevitUnitConverter`: mm/m/feet).                        |

Exemplos do estado atual:

```
Common/
├── Cad/CadGeometryExtractor.cs
├── Cad/CadSegmentExtractor.cs
├── Filters/CadCurveSelectionFilter.cs
├── Parameters/ParameterTextReader.cs
├── Parameters/RevitParameterReader.cs
├── Parameters/RevitParameterWriter.cs
├── Pipes/PipeConnectorService.cs
├── SharedParameters/SharedParameterDefinition.cs
├── SharedParameters/SharedParameterService.cs
├── SharedParameters/SharedParameterAccessor.cs
├── SharedParameters/SharedParameterFileService.cs
├── SharedParameters/ProjectParameterBindingService.cs
├── SharedParameters/SharedParameterEnsureResult.cs
├── SharedParameters/SharedParameterBindingKind.cs
├── SharedParameters/ExistingSharedParameterBindingInfo.cs
├── Transactions/RevitTransactionRunner.cs
├── Transactions/FailurePreprocessors/PipeCreationFailurePreprocessor.cs
└── Units/RevitUnitConverter.cs
```

## Features — implementação Revit por ferramenta

Cada ferramenta tem uma pasta `Features/<Nome>/` que reúne *só* o código
específico dela. As classes seguem a convenção:

| Sufixo               | Papel                                                          |
|----------------------|----------------------------------------------------------------|
| `Collector`          | Faz `FilteredElementCollector` com a categoria/tipo certos.    |
| `Reader`             | Lê dados específicos do projeto (descrição, segmento etc.).    |
| `Writer`             | Escreve um valor específico em um parâmetro.                   |
| `Creator`            | Cria elementos novos (tubo, extension, placeholder).           |
| `Applier`            | Orquestrador da feature (implementa `I<Nome>Service`).         |
| `Resolver`           | Resolve uma escolha do projeto (PipeType, Level, SystemType).  |
| `Finder`             | Encontra um elemento específico (ex.: conector vertical).      |
| `Filter` (selection) | Quando o filtro só faz sentido para essa feature.              |

Exemplos do estado atual:

```
Features/
├── TigreCodes/
│   ├── TigreCodeApplier.cs
│   ├── TigrePipeCollector.cs
│   ├── TigrePipeDataReader.cs
│   ├── TigreCodeWriter.cs
│   └── SharedParameters/TigreCodesSharedParameters.cs
├── PipeCadMapper/
│   ├── PipeCreator.cs
│   ├── PipeCreationResult.cs
│   ├── PipeConversionConfig.cs
│   ├── PipePlaceholderCreator.cs
│   └── PipePlaceholderConverter.cs
├── FloorDrainExtension/
│   ├── FloorDrainExtensionCreator.cs
│   ├── FloorDrainExtensionResult.cs
│   ├── VerticalConnectorFinder.cs
│   ├── FloorDrainExtensionPipeTypeResolver.cs
│   ├── FloorDrainExtensionSystemTypeResolver.cs
│   ├── FloorDrainExtensionLevelResolver.cs
│   └── FloorDrainExtensionPipeConnector.cs
├── BatchParameterEditor/
│   └── DisciplineFilters.cs
└── FamiliesImporter/
    └── RevitFamilyLoadOptions.cs
```

## Regra prática

> Se o código responde **"como esta ferramenta específica funciona?"**, vai
> em `Features/<Nome>`. Se responde **"como faço algo genérico no Revit?"**,
> vai em `Common`.

Casos de dúvida:

- Um `Collector` que coleta `OST_PipeCurves` e *só* faz sentido para uma
  ferramenta → `Features/<Nome>`.
- Um helper "ler texto de qualquer Parameter" → `Common/Parameters`.
- Um `ISelectionFilter` para CAD que qualquer ferramenta poderia usar →
  `Common/Filters`.
- Um `Resolver` que escolhe `PipeType` para *uma* ferramenta (com regras
  de nomenclatura próprias daquela feature) → `Features/<Nome>`.

## O que NÃO entra no Adapter

- **Application/UseCases** — orquestração de regra de aplicação.
- **Domain** — regras puras (sem RevitAPI).
- **Plugin** — `IExternalCommand`, `RibbonButtonDefinition`, ViewModels,
  janelas WPF.
- **ExternalEvents acoplados a UI** — ficam no Plugin (ADR-0003).
- **Infrastructure** — persistência local, HTTP, licenciamento, telemetria.

## Como adicionar coisas novas

1. **Helper reutilizável (mais de uma ferramenta vai usar):** crie em
   `Common/<categoria>/<Nome>.cs`.
2. **Comportamento específico de uma ferramenta:** crie em
   `Features/<Nome>/<Nome><Sufixo>.cs`.
3. **Shared parameter novo:** declare em
   `Features/<Nome>/SharedParameters/<Nome>SharedParameters.cs` e use
   `SharedParameterService` para a parte genérica. Detalhes em
   `shared-parameters-architecture.md`.
4. **Falha do Revit que precisa ser tratada:** crie um
   `IFailuresPreprocessor` em `Common/Transactions/FailurePreprocessors/`.
