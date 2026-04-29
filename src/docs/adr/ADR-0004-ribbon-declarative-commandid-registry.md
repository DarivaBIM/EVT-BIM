# ADR-0004 — Ribbon declarativa + `RibbonCommandId` + `CommandRegistry`

## Status
Aceito.

## Contexto
A versão original do plugin construía a ribbon de forma imperativa em
`App.OnStartup`, instanciando `PushButtonData` com strings literais para
`FullClassName`. Isso amarrava a ribbon ao tipo concreto da classe de comando,
duplicava texto e tooltip, dificultava localização e impossibilitava customizar
a ribbon por licença/cliente.

## Decisão
1. Definir um **enum estável** `RibbonCommandId` em `Revit.Abstractions`
   (`ImportFamilies`, `CalculatePressure`, `WriteTigreCodes`, etc.). Esse enum
   nunca muda nome para um id já existente — só ganha novos valores.
2. Cada plugin de versão fornece um `CommandRegistry : ICommandRegistry` que
   mapeia `RibbonCommandId → Type` (a classe `IExternalCommand` daquela
   versão).
3. A ribbon é **declarativa** — uma `RibbonDefinition` contendo
   `RibbonPanelDefinition` que contém `RibbonButtonDefinition` (id interno,
   texto, tooltip, longDescription, ícones, link de ajuda, requisito de
   licença, estilo de botão). Cada `RibbonButtonDefinition` aponta para um
   `RibbonCommandId`, não para um `Type`.
4. Um `RibbonBuilder` em `Revit.Hosting` traduz `RibbonDefinition` em
   `PushButtonData` resolvendo o `Type` via `ICommandRegistry`.

## Consequências
- Adicionar uma nova ferramenta é: criar a classe `IExternalCommand`, registrar
  no `CommandRegistry` e listar um `RibbonButtonDefinition` em
  `DarivaBimRibbonDefinition`.
- A ribbon pode ser carregada dinamicamente (ex.: filtrada por licença) sem
  mudar `App.cs`.
- Pronto para localização: `RibbonButtonDefinition.LocalizationKey` permite
  resolver `Text/ToolTip/LongDescription` por idioma.
- Strings literais como nomes de classe de comando deixaram de ser fonte de
  bug.
