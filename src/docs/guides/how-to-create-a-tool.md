# Como criar uma nova ferramenta

Este guia cobre o caminho recomendado para adicionar uma ferramenta nova ao
plugin DarivaBIM seguindo a organização por feature
(ADR-0011) sem violar a Clean Architecture (ADR-0001/0003).

## Onde cada coisa mora

```
src/Plugins/DarivaBIM.Plugin.V2026/Features/<NomeDaFerramenta>/
├── <Nome>Button.cs                # RibbonButtonDefinition
├── <Nome>Command.cs               # IExternalCommand (casca fina)
├── <Nome>Tool.cs                  # opcional — orquestração Adapter↔UseCase
├── <Nome>ExternalEvent.cs         # opcional — modeless / pick lifecycle
├── <Nome>Handler.cs               # opcional — IExternalEventHandler
└── <Nome>Feature.cs               # manifesto (CommandId, Button, CommandType, AddServices)
```

A regra de negócio fica nas camadas certas:

- `DarivaBIM.Domain` — regras puras (sem RevitAPI, sem WPF).
- `DarivaBIM.Application` — `UseCases/`, `Contracts/` (interfaces) e `DTOs/`.
- `DarivaBIM.Revit.Adapters.V2026` — implementações concretas que falam com
  RevitAPI (Repositories, Writers, Readers, Mapping, Filters etc.).
- `DarivaBIM.Infrastructure.*` — persistência, API, licenciamento, telemetria.
- `DarivaBIM.Presentation.Wpf` — Views/ViewModels neutros (sem RevitAPI).

## Ferramenta simples (sem regra de negócio)

1. Adicionar um valor novo em `RibbonCommandId`
   (`src/Revit/DarivaBIM.Revit.Abstractions/Ribbon/RibbonCommandId.cs`) se a
   ação ainda não existir.
2. Criar a pasta `src/Plugins/DarivaBIM.Plugin.V2026/Features/<NomeDaFerramenta>/`.
3. Criar `<Nome>Button.cs` — uma classe estática com uma propriedade
   `Definition` retornando um `RibbonButtonDefinition` (texto, tooltip,
   ícones, `commandId: RibbonCommandId.<NovoId>`, `LicenseRequirement`).
4. Criar `<Nome>Command.cs` — `IExternalCommand` que delega para
   `App.Executor.Execute` (padrão das outras features).
5. Criar `<Nome>Feature.cs` — manifesto da feature:
   ```csharp
   public static RibbonCommandId CommandId => RibbonCommandId.<NovoId>;
   public static RibbonButtonDefinition Button => <Nome>Button.Definition;
   public static Type CommandType => typeof(<Nome>Command);
   public static IServiceCollection AddServices(IServiceCollection services)
       => services;
   ```
6. Adicionar `<Nome>Feature.Button` em
   `Ribbon/Panels/TigrePanelDefinition.cs`.
7. Adicionar `{ <Nome>Feature.CommandId, <Nome>Feature.CommandType }` em
   `Ribbon/CommandRegistry.cs`.
8. Adicionar `<Nome>Feature.AddServices(services)` em
   `Composition/PluginFeatureServiceRegistration.cs`.

## Ferramenta com regra de negócio

Além dos passos acima:

1. Criar o UseCase em
   `src/Core/DarivaBIM.Application/UseCases/<Nome>/<Nome>UseCase.cs`. O
   construtor recebe interfaces; `Execute` retorna um DTO de resultado.
2. Criar o contrato em `Application.Contracts` (`I<Nome>Service`,
   `I<Nome>Repository`, `I<Nome>Provider`, conforme o papel).
3. Criar DTOs em `Application.DTOs/<Categoria>/`.
4. Implementar a interface em `Revit.Adapters.V2026/<Categoria>/<Nome>Reader.cs`
   (ou `Writer/Repository/Mapper/Provider`, conforme o papel).
5. Criar regras puras em `Domain/<Categoria>/` se houver.
6. Criar loader/API/persistência em `Infrastructure.*` se necessário.
7. Criar `<Nome>Tool.cs` na pasta da feature para juntar Adapter+UseCase
   (precisa do `Document` ativo, mostra `TaskDialog`, etc.). Registrar via
   `<Nome>Feature.AddServices`.
8. Resolver a Tool no Command (`ctx.Services.GetService(typeof(<Nome>Tool))`).
9. Cobrir o UseCase com testes em `src/tests/DarivaBIM.Application.Tests`
   usando fakes/in-memory para os adapters.

## Regra de ouro

Cada ferramenta tem **uma pasta central** no Plugin
(`Plugin.V2026/Features/<Nome>`), mas a regra de negócio continua nas
camadas certas (Domain / Application / Adapter / Infrastructure /
Presentation.Wpf). A pasta `Features/` organiza apenas a porta de entrada
no Revit.
