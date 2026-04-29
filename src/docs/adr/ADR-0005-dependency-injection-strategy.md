# ADR-0005 — Estratégia de Dependency Injection

## Status
Aceito.

## Contexto
Antes, cada `IExternalCommand` instanciava manualmente seus colaboradores
(`new TigreCodeApplier`, `new HttpClient` etc.) e mantinha singletons via
`static`. Isso impedia testar, escondia dependências e amarrava o ciclo de
vida ao Revit.

## Decisão
1. **Container global** — montado uma única vez em
   `App.OnStartup` via `Microsoft.Extensions.DependencyInjection`. Ele vive
   enquanto o Revit estiver aberto e é descartado em `OnShutdown`.
2. **Escopo por comando** — cada `IExternalCommand.Execute` chama
   `App.Executor.Execute(...)`, que cria um `IServiceScope` novo. Tudo o que
   o use case ou os adapters precisarem é resolvido a partir do
   `ctx.Services` desse escopo, garantindo isolamento entre execuções.
3. **UseCases recebem dependências via construtor** — nunca usam
   `ServiceLocator` ou `static`. O `IExternalCommand` só busca no scope o que
   é estritamente Revit-específico (ex.: o `Document` corrente).

## Fluxo
```
App.OnStartup
   ServiceCollection
   └── PluginHost (root provider)

Command.Execute
   RevitCommandExecutor.Execute(commandData, action)
   ├── using IServiceScope
   ├── new RevitCommandContext(commandData, scope.ServiceProvider, RevitDocumentContext)
   └── action(ctx) // resolve UseCase, executa, captura exceções
```

## Consequências
- Testes da camada de Application só precisam mockar/fakeear interfaces — o
  Revit nunca é instanciado.
- Exceções viram `Result.Failed` automaticamente; cancelamentos viram
  `Result.Cancelled`.
- Não há mais singletons globais espalhados; o único estático restante é
  `App.Executor`, exposto exclusivamente para os `IExternalCommand` poderem
  acessá-lo (eles são instanciados pelo Revit, não pelo DI).
