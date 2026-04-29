# ADR-0011 — Organização por Feature dentro de Plugin.Vxxxx

- Status: Aceito
- Data: 2026-04-29

## Contexto

Após o ADR-0010 o `Plugin.V2026` já era uma casca fina, mas as portas de
entrada de cada ferramenta ficavam espalhadas por três pastas:

```
Plugin.V2026/
├── Commands/             # IExternalCommand de todas as ferramentas
├── Ribbon/Buttons/       # RibbonButtonDefinition de todas as ferramentas
├── ExternalServices/     # ExternalEvent + Handler de todas as ferramentas
└── Tools/                # glue Adapter↔UseCase
```

Para enxergar tudo o que pertence à ferramenta "Códigos Tigre" era preciso
abrir quatro pastas e correlacionar arquivos pelo nome. Adicionar ou remover
uma ferramenta exigia cirurgia em vários diretórios distantes entre si, e
nada na estrutura indicava qual `Button.cs` ia com qual `Command.cs` ou com
qual `ExternalEvent.cs`.

## Decisão

1. **Cada ferramenta tem uma pasta central em
   `Plugin.Vxxxx/Features/<Nome>`.** Essa pasta agrupa tudo que é a "porta
   de entrada" da ferramenta no Revit:
   - `<Nome>Button.cs` — `RibbonButtonDefinition`.
   - `<Nome>Command.cs` — `IExternalCommand` (casca fina).
   - `<Nome>Tool.cs` — orquestração Adapter↔UseCase quando faz sentido (ex.:
     `ApplyTigreCodesTool`).
   - `<Nome>ExternalEvent.cs` / `<Nome>Handler.cs` — `ExternalEvent` e seu
     `IExternalEventHandler` quando a ferramenta tem ciclo modeless.
   - `<Nome>Feature.cs` — manifesto que expõe `CommandId`, `Button`,
     `CommandType` e `AddServices(IServiceCollection)` para o resto do
     plugin consumir.

2. **A regra de negócio continua nas camadas certas.** Features NÃO
   substituem Application, Domain, Adapter ou Infrastructure. As mesmas
   regras dos ADR-0001 e ADR-0003 valem:
   - Domain: regras puras (sem RevitAPI, sem WPF).
   - Application: UseCases, Contracts e DTOs (sem RevitAPI).
   - Revit.Adapters.Vxxxx: implementações concretas com RevitAPI.
   - Infrastructure.*: persistência, API, licenciamento, telemetria.
   - Presentation.Wpf: Views/ViewModels neutros (sem RevitAPI).
   - Plugin.Vxxxx: composição + portas de entrada por feature.

3. **Painel e CommandRegistry consomem o manifesto.** Em vez de o painel
   importar diretamente `XxxButton.Definition` e o registry mapear
   `RibbonCommandId → typeof(XxxCommand)`, ambos passam pelo `Feature.cs`.
   Isso centraliza os três fios (id, botão, tipo de comando) em um único
   lugar por ferramenta.

4. **DI por feature.** Cada `Feature.cs` expõe `AddServices(IServiceCollection)`.
   `Composition/PluginFeatureServiceRegistration.cs` chama todas as features
   em sequência; `App.cs` chama essa única extensão. Adicionar serviços de
   uma ferramenta nova não toca `App.cs`.

## Consequências

- Para entender uma ferramenta basta abrir uma pasta. Para criar uma nova
  basta espelhar o esqueleto de outra.
- Painel e CommandRegistry passam a depender dos manifestos `Feature.cs`,
  reduzindo a chance de uma ferramenta ficar com botão na ribbon mas sem
  comando registrado (ou vice-versa).
- A pasta `Plugin.Vxxxx/Tools/` continua existindo só para helpers
  reutilizáveis entre ferramentas (ex.: `RevitElementIdConversions`,
  `PipeConversionConfigFactory`, `PipeInsertionStatusFormatter` no caso do
  PipeCadMapper). A glue específica de uma ferramenta vai para
  `Features/<Nome>/<Nome>Tool.cs`.
- `ExternalEventHandler`s acoplados à UI continuam no Plugin (ADR-0003); o
  que muda é que ficam ao lado do command e do botão da mesma ferramenta.

## Pendências reconhecidas

- `Tools/PipeCadMapper/` ainda contém helpers usados só pelo PipeCADMapper
  (`PipeConversionConfigFactory`, `PipeInsertionStatusFormatter`,
  `RevitElementIdConversions`). Mover para `Features/PipeCadMapper/` é
  trivial mas foge ao escopo desta ADR; preserva-se a estrutura para evitar
  churn em arquivos consumidos por handlers já no novo lugar.
- `Ui/` ainda hospeda Views/ViewModels que poderiam ser repartidos entre
  `Presentation.Wpf` e as features. ADR-0010 já trata da pendência de mover
  o `ParameterEditorViewModel` (que vaza `StorageType`/`Discipline`) para
  Presentation.Wpf.

## Referências

- ADR-0001 — Clean architecture
- ADR-0003 — Plugin vs Adapter
- ADR-0004 — Ribbon declarativa + CommandRegistry
- ADR-0005 — Estratégia de DI
- ADR-0009 — Convenções de nomenclatura
- ADR-0010 — Plugin fino, Composition Root e Presentation.Wpf neutra
