# ADR-0010 — Plugin.Vxxxx fino, Composition Root e Presentation.Wpf neutra

- Status: Aceito
- Data: 2026-04-29

## Contexto

Após a refatoração da Ribbon (ADR-0004), o `Plugin.V2026` ainda concentrava
responsabilidades pesadas: ViewModels com tipos do RevitAPI, montagem manual
de UseCases dentro de `IExternalCommand`, `ExternalEventHandler`s longos,
registros de DI espalhados em `App.cs`. A consequência foi um único
projeto que misturava entrada do Revit, regras de UI, mapeamento Adapter→Use
case e composição. Isso é difícil de portar para V2025 / V2027 e dificulta
testar partes em isolamento.

## Decisão

1. **Plugin.Vxxxx é Composition Root + ponte para o Revit.** O projeto
   versionado contém:
   - `App.cs` (IExternalApplication, mínimo).
   - `Composition/` com um arquivo por camada
     (`PluginServiceRegistration`, `ApplicationServiceRegistration`,
     `InfrastructureServiceRegistration`,
     `RevitAdapterServiceRegistration`, `PresentationServiceRegistration`).
   - `Commands/` com `IExternalCommand` em forma de **casca fina**
     (validação + resolução de DI + delegação para `Tools/`).
   - `Tools/` com a glue por ferramenta (ex.: `ApplyPipeCodesTool`,
     `PipeCadMapper/PipeConversionConfigFactory`,
     `PipeCadMapper/PipeInsertionStatusFormatter`,
     `PipeCadMapper/RevitElementIdConversions`).
   - `ExternalServices/` com `ExternalEvent` + `Handler` magros, que
     delegam montagem de configs/textos para `Tools/`.

2. **Presentation.Wpf é neutra.** ViewModels e modelos reutilizáveis vivem
   em `DarivaBIM.Presentation.Wpf`. Esses tipos:
   - **não** podem referenciar `Autodesk.Revit.*`;
   - representam IDs de elementos como `long` (DTO neutro);
   - herdam de uma base `Common.ObservableObject` para o boilerplate de
     `INotifyPropertyChanged`.
   A conversão `long ↔ ElementId` acontece em helpers do Plugin/Adapter
   (ex.: `RevitElementIdConversions` em `Plugin.V2026/Tools/PipeCadMapper`).

3. **Adapters fragmentados por responsabilidade.** Classes-fachada
   (`PipeCreator`) permanecem como porta de entrada estável, mas a lógica
   nova segue para `Cad/`, `Pipes/`, `Selection/`, etc. — uma classe por
   responsabilidade.

4. **Testes de arquitetura.** Um projeto `DarivaBIM.Architecture.Tests`
   varre o código-fonte e falha quando:
   - `Domain` ou `Application` referenciam `Autodesk.Revit.*`;
   - `Application` referencia WPF (`System.Windows.*`);
   - `Presentation.Wpf` referencia `Autodesk.Revit.*`.

## Consequências

- O `Plugin.V2026` fica menor, focado em entrada do Revit. Adicionar uma
  nova ferramenta significa criar um command + uma classe em `Tools/` —
  sem mexer na composição global.
- Portar para V2027 fica mais barato: copiar o esqueleto e ajustar o
  Adapter; a Presentation, Application e Domain seguem inalteradas.
- ViewModels podem ser testados sem RevitAPI (são Revit-agnósticos).
- ExternalEvents permanecem responsáveis pelo ciclo de vida `Idling/Pick`,
  mas não acumulam mais formatação de strings ou montagem de DTOs do
  Adapter.
- Há um custo: dois saltos extra no fluxo (Command → Tool → UseCase),
  e o Plugin precisa expor helpers de conversão `long ↔ ElementId` por
  versão. O ganho de testabilidade e separação justifica.

## Pendências reconhecidas

- `BatchParameterEditorViewModel` ainda usa `StorageType` (RevitAPI) e o enum
  `Discipline` (Adapter); migrá-lo para `Presentation.Wpf` exige extrair
  esses tipos para um espaço neutro. Trabalho da próxima leva.
- `PipeCreator` continua sendo uma fachada estática com transação. A
  próxima etapa quebra `PipePlaceholderService` e `PipeCreationService`.
- A unificação de `<TargetFramework>` para `$(RevitTargetFramework)` em
  todos os csprojs versionados depende de revisar a ordem de avaliação
  das `Directory.Build.props` para não quebrar a build.

## Referências

- ADR-0001 — Clean architecture (Domain agnóstico)
- ADR-0002 — Estratégia multi-versão Revit
- ADR-0003 — Plugin vs Adapter
- ADR-0004 — Ribbon declarativa + CommandRegistry
- ADR-0005 — Estratégia de DI
- ADR-0007 — Camada Hosting
- ADR-0009 — Convenções de nomenclatura
