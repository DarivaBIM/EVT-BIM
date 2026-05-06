# DarivaBIM — Visão geral da arquitetura

> Este repositório serve hoje ao plugin **EVT-BIM** (entregável Tigre, no
> escopo do programa Engenharia de Valor Tigre) e servirá ao **DarivaBIM
> V2** no futuro, compartilhando exatamente a mesma arquitetura descrita
> neste documento. A diferença entre os dois entregáveis está apenas na
> "casca" externa (aba do Revit, `.addin`, instalador, mensagens visíveis
> ao usuário) — projetos C#, namespaces e assemblies permanecem
> `DarivaBIM.*`. Ver [ADR-0014](../adr/ADR-0014-evt-bim-rebrand.md).

## 1. Camadas e dependências

```
┌──────────────────────────────────────────────────────────────────┐
│                        Plugin.Vxxxx                             │
│         (IExternalApplication, IExternalCommand, .addin)        │
│         ▲ ▲                                          ▲          │
│         │ └── refs ── Presentation.Wpf               │          │
│         └── refs ── Revit.Adapters.Vxxxx ─── refs ── Hosting    │
└────────────────────────────────────┬─────────────────────────────┘
                                     │
              refs                   ▼
┌──────────────────────────────────────────────────────────────────┐
│                   Revit.Abstractions  +  Hosting                 │
│   (interfaces neutras + DI + ExternalEvent/Idling/Ribbon)        │
└──────────────────────────────────────────────────────────────────┘
                                     ▲
                                     │
                ┌────────────────────┴──────────────────┐
                │              Application              │
                │ (UseCases, Contracts, DTOs)           │
                └────────────────────┬──────────────────┘
                                     ▼
                ┌──────────────────────────────────────┐
                │                Domain                │
                │ (entidades, VOs, regras puras)       │
                └──────────────────────────────────────┘
```

Direção das setas = "depende de". Tudo aponta para baixo. Nada da camada
inferior conhece a superior.

## 2. Regras de não-vazamento da RevitAPI

Os tipos `Document`, `Element`, `ElementId`, `Connector`, `FamilyInstance`,
`BuiltInParameter`, `Transaction`, `UIApplication`, `UIDocument`,
`TaskDialog` **não podem** aparecer em:
- `DarivaBIM.Domain`
- `DarivaBIM.Application`
- `DarivaBIM.Presentation.Wpf`

Eles só podem aparecer em:
- `DarivaBIM.Revit.Adapters.Vxxxx`
- `DarivaBIM.Plugin.Vxxxx`
- `DarivaBIM.Revit.Hosting`

`Revit.Abstractions` define interfaces neutras (`IRevitDocumentContext`,
`IRevitTransactionRunner`, `IRevitParameterWriter`, `IRevitSelectionService`)
que substituem esses tipos quando o Core precisa falar com o host.

## 3. Fluxo de uma ferramenta

```
Usuário clica botão da Ribbon
    │
    ▼
Revit instancia a IExternalCommand (ex.: ApplyPipeCodesCommand)
    │
    ▼
Command.Execute → App.Executor.Execute(commandData, ref message, ctx => ...)
    │
    ├── using IServiceScope = host.CreateScope()
    │
    ├── new RevitCommandContext(commandData, scope.ServiceProvider, RevitDocumentContext(doc))
    │
    └── action(ctx):
        ├── resolve dependências do ctx.Services
        ├── instancia o Adapter Revit concreto (ex.: TigreCodeApplier)
        ├── instancia o UseCase (ex.: ApplyTigreCodesUseCase)
        ├── chama useCase.Execute()
        │       │
        │       └── usa ITigreCatalogProvider (Domain), Domain.TigreCatalog
        │           e a interface ITigreCodeApplyService (Application)
        │
        └── retorna Result.Succeeded / Failed / Cancelled
```

## 4. Organização da Ribbon

```
DarivaBimRibbonDefinition (agregador)
├── Panels/                       (um arquivo por painel)
│   └── TigrePanelDefinition.cs
└── Features/<Tool>/<Tool>Button.cs (botão ao lado da feature)

RibbonButtonDefinition (commandId: RibbonCommandId.WritePipeCodes)
                  │
                  ▼
       ICommandRegistry (Plugin.Vxxxx)
                  │
                  ▼
       typeof(ApplyPipeCodesCommand)
```

Adicionar uma ferramenta nova significa (ver também
`src/docs/guides/how-to-create-a-tool.md` e ADR-0011):
1. Acrescentar um valor em `RibbonCommandId` (se for novo conceito).
2. Criar a pasta `Plugin.Vxxxx/Features/<Nome>/` com `<Nome>Button.cs`,
   `<Nome>Command.cs` e `<Nome>Feature.cs`.
3. Adicionar `<Nome>Feature.Button` no painel
   (`Ribbon/Panels/<Nome>PanelDefinition.cs`).
4. Adicionar `{ <Nome>Feature.CommandId, <Nome>Feature.CommandType }` no
   `Ribbon/CommandRegistry.cs`.
5. Adicionar `<Nome>Feature.AddServices(services)` no
   `Composition/PluginFeatureServiceRegistration.cs` se houver serviços.
6. Se a ferramenta tem regra de negócio, criar UseCase em
   `DarivaBIM.Application.UseCases.<Nome>/`, contratos em
   `Application.Contracts` e implementação em
   `DarivaBIM.Revit.Adapters.V2026/...`.

Sem mudar `App.cs`, sem mexer em `RibbonBuilder`.

## 4.1. Plugin.Vxxxx como casca fina (organização por feature)

A partir de ADR-0010 (plugin fino) + ADR-0011 (organização por feature), a
estrutura interna do plugin V2026 segue o padrão:

```
DarivaBIM.Plugin.V2026/
├── App.cs                        (IExternalApplication, magro)
├── Composition/                  (Composition Root, dividido por camada)
│   ├── PluginServiceRegistration.cs
│   ├── ApplicationServiceRegistration.cs
│   ├── InfrastructureServiceRegistration.cs
│   ├── RevitAdapterServiceRegistration.cs
│   ├── PresentationServiceRegistration.cs
│   └── PluginFeatureServiceRegistration.cs   (agrega Feature.AddServices)
├── Features/                     (uma pasta por ferramenta)
│   ├── PipeCodes/
│   │   ├── PipeCodesButton.cs
│   │   ├── PipeCodesFeature.cs
│   │   ├── ApplyPipeCodesCommand.cs
│   │   └── ApplyPipeCodesTool.cs
│   ├── PipeCadMapper/
│   │   ├── PipeCadMapperButton.cs
│   │   ├── PipeCadMapperFeature.cs
│   │   ├── ShowPipeConverterCommand.cs
│   │   ├── PipeInsertionExternalEvent.cs
│   │   ├── PipeInsertionHandler.cs
│   │   ├── PipeConverterDataLoadExternalEvent.cs
│   │   └── PipeConverterDataLoadHandler.cs
│   ├── FloorDrainExtension/
│   ├── BatchParameterEditor/
│   └── FamiliesImporter/
├── Tools/                        (helpers reutilizáveis entre features)
│   └── PipeCadMapper/
├── Ribbon/                       (DarivaBimRibbonDefinition, painéis, CommandRegistry)
└── Ui/                           (windows/pages WPF do plugin)
```

Cada `IExternalCommand` faz apenas:
1. Abre escopo do executor (`App.Executor.Execute`).
2. Valida `Document` (existe? não-família?).
3. Resolve dependências do `ctx.Services`.
4. Delega a execução para a Tool da feature (ex.: `ApplyPipeCodesTool`).
5. Traduz o resultado em `Result.Succeeded/Cancelled/Failed`.

## 4.1.1. Organização por Feature no Plugin

Cada ferramenta tem uma pasta central em `Plugin.Vxxxx/Features/<Nome>/`,
com:

- `<Nome>Button.cs` — `RibbonButtonDefinition`.
- `<Nome>Command.cs` — `IExternalCommand` (casca fina).
- `<Nome>Tool.cs` — orquestração Adapter↔UseCase quando faz sentido.
- `<Nome>ExternalEvent.cs` / `<Nome>Handler.cs` — quando há ciclo modeless.
- `<Nome>Feature.cs` — manifesto: `CommandId`, `Button`, `CommandType`,
  `AddServices`.

A regra de negócio **continua nas camadas certas**: Feature não substitui
Application, Domain, Adapter, Infrastructure ou Presentation.Wpf. O objetivo
é apenas agrupar a porta de entrada da ferramenta no Revit em um único lugar
para melhorar navegação e entendimento.

`Ribbon/Panels/TigrePanelDefinition.cs` consome `Feature.Button` e
`Ribbon/CommandRegistry.cs` consome `Feature.CommandId/CommandType`, então a
relação id↔botão↔comando vive em um único arquivo por ferramenta.

Detalhes completos em `src/docs/guides/how-to-create-a-tool.md` e em
ADR-0011.

## 4.2. Presentation.Wpf — view models neutros

`DarivaBIM.Presentation.Wpf` hospeda Views e ViewModels reutilizáveis entre
versões do Revit. As regras invioláveis:

- Não pode usar `Autodesk.Revit.*`.
- Não pode usar `ElementId`, `Document`, `UIDocument`, `UIApplication`,
  `Connector`, `BuiltInParameter` etc.
- Identificadores de elementos viajam como `long` (DTO neutro). A conversão
  `long ↔ ElementId` acontece apenas no Plugin/Adapter, idealmente em um
  helper `RevitElementIdConversions` por versão (Revit 2024+ usa
  `ElementId.Value`; pré-2024 usa `ElementId.IntegerValue`).

Estrutura recomendada:

```
DarivaBIM.Presentation.Wpf/
├── Common/
│   └── ObservableObject.cs       (base INotifyPropertyChanged)
├── Models/                       (DTOs neutros: Option/Item ViewModels)
│   ├── PipingSystemOptionViewModel.cs
│   ├── PipeTypeOptionViewModel.cs
│   └── LevelOptionViewModel.cs
└── <Feature>/                    (pastas por feature WPF)
    └── <Feature>ViewModel.cs
```

## 4.3. ExternalEvents

Um `IExternalEventHandler` é uma ponte para o contexto Revit (UIApplication +
PickObject + Transaction). Não é o lugar para regra pesada. O padrão atual:

- O Handler controla o ciclo (pick → reagendar → cancelamento).
- A montagem do `PipeConversionConfig` saiu para
  `Plugin.V2026/Features/PipeCadMapper/Tools/PipeConversionConfigFactory`.
- A formatação da mensagem de status saiu para
  `Plugin.V2026/Features/PipeCadMapper/Tools/PipeInsertionStatusFormatter`.
- O ViewModel consumido vem de `Presentation.Wpf` e não conhece `ElementId`.

## 4.4. Organização por Feature no Adapter

A partir de ADR-0012, `Revit.Adapters.V2026` é organizada em **Common +
Features**, espelhando a divisão do Plugin (ADR-0011):

```
DarivaBIM.Revit.Adapters.V2026/
├── Common/                            (blocos reutilizáveis de RevitAPI)
│   ├── Cad/
│   ├── Elements/
│   ├── Filters/
│   ├── Parameters/
│   ├── Pipes/
│   ├── Selection/
│   ├── SharedParameters/
│   ├── Transactions/
│   │   └── FailurePreprocessors/
│   └── Units/
└── Features/                          (implementação Revit por ferramenta)
    ├── TigreCodes/
    │   └── SharedParameters/
    ├── PipeCadMapper/
    ├── FloorDrainExtension/
    ├── BatchParameterEditor/
    └── FamiliesImporter/
```

Princípios:

- **Common**: helpers que respondem a "como faço X genérico no Revit?"
  (`SharedParameterService`, `RevitParameterReader`, `RevitTransactionRunner`,
  `RevitUnitConverter`, `CadCurveSelectionFilter`, `PipeConnectorService` etc.).
- **Features**: classes que respondem a "como esta ferramenta funciona?"
  (`TigreCodeApplier`, `PipeCreator`, `FloorDrainExtensionCreator`, `RevitFamilyLoadOptions`,
  `Discipline*`).

Detalhes em `src/docs/guides/adapter-anatomy.md` e
`src/docs/guides/shared-parameters-architecture.md`.

## 4.5. Fluxo Dynamo/Python → Plugin

Quando uma ferramenta nasce como script Dynamo/Python:

```
Script Dynamo/Python
   → docs/dynamo-migration/<Nome>.md          (etiquetar trechos do script)
   → Plugin.V2026/Features/<Nome>/             (botão, command, tool, events)
   → Application/UseCases/<Nome>/              (se houver orquestração reusável)
   → Adapter.V2026/Features/<Nome>/            (collectors, readers, writers, creators)
   → Domain/                                   (regras puras, se houver)
   → Infrastructure.*                          (persistência, API, telemetria)
```

Etiquetas do roteiro: `[INPUT]`, `[SELECTION]`, `[COLLECT]`, `[READ_PARAM]`,
`[FILTER]`, `[CALCULATE]`, `[WRITE_PARAM]`, `[CREATE_ELEMENT]`,
`[TRANSACTION]`, `[RESULT]`, `[UI]`. Guia completo em
`src/docs/guides/dynamo-to-plugin-migration.md`; template em
`src/docs/dynamo-migration/_template.md`; exemplos prontos em
`src/docs/dynamo-migration/{tigre-codes,pipe-cad-mapper,floor-drain-extension}.md`.

## 5. Multi-versão Revit

| Versão | TFM             | Status atual |
|--------|-----------------|--------------|
| 2023   | net48           | Stub (csproj + Placeholder.cs) |
| 2024   | net48           | Stub |
| 2025   | net8.0-windows  | Stub |
| 2026   | net8.0-windows  | **Produção** |
| 2027   | net10.0-windows | Stub |

`Domain`, `Application`, `Revit.Abstractions` e `Infrastructure.*` usam
`netstandard2.0` para serem consumíveis pelos plugins net48, net8 e net10.

`Hosting`, `Adapters.Vxxxx` e `Plugin.Vxxxx` usam o TFM da versão.

## 6. Como adicionar uma nova versão do Revit

1. Criar `src/Revit/DarivaBIM.Revit.Adapters.V20XX/` copiando o V2026.
2. Criar `src/Plugins/DarivaBIM.Plugin.V20XX/` copiando o V2026.
3. Trocar `<RevitVersion>2026</RevitVersion>` para `<RevitVersion>20XX</RevitVersion>`
   nos dois csprojs.
4. Ajustar TFM se necessário (`net48` para 2023/2024, `net10.0-windows` para
   2027+).
5. Criar `src/Build/AddinManifests/DarivaBIM.V20XX.addin`.
6. Adicionar os 2 projetos novos no `DarivaBIM.sln`.
7. Quebrar o que mudou na API; idealmente os tests do Adapter pegam.

## 7. Como adicionar uma nova ferramenta/botão

Resumo (passo a passo completo em
`src/docs/guides/how-to-create-a-tool.md`):

1. Definir um valor novo em `RibbonCommandId` se a ação não existir.
2. Criar `Plugin.V2026/Features/<Nome>/` com `<Nome>Button.cs`,
   `<Nome>Command.cs` e `<Nome>Feature.cs`.
3. Adicionar `<Nome>Feature.Button` em
   `Ribbon/Panels/TigrePanelDefinition.cs` e
   `{ <Nome>Feature.CommandId, <Nome>Feature.CommandType }` em
   `Ribbon/CommandRegistry.cs`.
4. Adicionar `<Nome>Feature.AddServices(services)` em
   `Composition/PluginFeatureServiceRegistration.cs` se a feature registrar
   serviços.
5. Se a ferramenta tem regra de negócio: criar UseCase em
   `DarivaBIM.Application.UseCases.<Nome>/` que recebe interfaces por
   construtor; implementar as interfaces em
   `DarivaBIM.Revit.Adapters.V2026/...` ou `DarivaBIM.Infrastructure.*`;
   adicionar `<Nome>Tool.cs` na pasta da feature para colar tudo.

## 8. Como criar um novo UseCase

1. `DarivaBIM.Application/UseCases/<Nome>/<Nome>UseCase.cs`.
2. Constructor recebe interfaces (`I*Service`, `I*Repository`, `I*Provider`).
3. Método `Execute(...)` retorna DTOs (`*Result`).
4. Sem qualquer `Autodesk.Revit.*`, `WPF` ou `HttpClient`.
5. Teste em `DarivaBIM.Application.Tests` com fakes/in-memory.

## 9. Como criar um novo adapter Revit

1. Decida onde mora a classe nova:
   - **Reutilizável por várias features** → `Adapters.V2026/Common/<categoria>/`
     (exemplos: `Common/SharedParameters/SharedParameterService.cs`,
     `Common/Parameters/RevitParameterReader.cs`,
     `Common/Transactions/RevitTransactionRunner.cs`).
   - **Específica de uma ferramenta** → `Adapters.V2026/Features/<Nome>/`
     com sufixos `Collector`, `Reader`, `Writer`, `Creator`, `Resolver`,
     `Finder`, `Applier` (exemplos: `Features/TigreCodes/TigreCodeApplier.cs`,
     `Features/FloorDrainExtension/VerticalConnectorFinder.cs`).
2. Se implementa um contrato de Application (`I*Service`/`I*Repository`/
   `I*Provider`), recebe `Document` (ou `IRevitDocumentContext`) por
   construtor.
3. Encapsula transações via `RevitTransactionRunner`, leitura/escrita de
   parâmetros via `RevitParameterReader`/`RevitParameterWriter` e conversões
   via `RevitUnitConverter`, mantendo cada classe focada em uma única
   responsabilidade.
4. Para shared parameters, declare a definição em
   `Features/<Nome>/SharedParameters/<Nome>SharedParameters.cs` e use
   `Common/SharedParameters/SharedParameterService` — não reimplemente
   criação/binding (ver ADR-0013).
5. Testar com smoke tests em `DarivaBIM.Revit.Adapters.V2026.Tests`.

## 10. Como compilar e testar V2026

```cmd
:: Pré-requisito: Revit 2026 instalado em "C:\Program Files\Autodesk\Revit 2026"
:: ou variável de ambiente REVIT_2026_PATH apontando para a instalação.

dotnet restore DarivaBIM.sln
dotnet build src\Plugins\DarivaBIM.Plugin.V2026\DarivaBIM.Plugin.V2026.csproj -c Debug
```

O target `DeployAddin` (no csproj do Plugin.V2026) chama
`build/deploy_revit_2026.cmd`, que copia o `.addin` para
`%ProgramData%\Autodesk\Revit\Addins\2026\` e os assemblies para a subpasta
`DarivaBIM\`.

Testes unitários (Domain + Application + Abstractions) podem rodar em
qualquer máquina Windows com .NET 8 SDK:
```cmd
dotnet test src\tests\DarivaBIM.Domain.Tests
dotnet test src\tests\DarivaBIM.Application.Tests
dotnet test src\tests\DarivaBIM.Revit.Abstractions.Tests
```

Testes de adapter exigem a RevitAPI e só rodam em máquinas com Revit
instalado.
