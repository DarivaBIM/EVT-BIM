# ADR-0007 — Camada de Hosting Revit

## Status
Aceito.

## Contexto
A integração com o Revit envolve mecânica que não é regra de negócio nem
chamada típica de API: registrar `ExternalEvent`, lidar com `Idling`,
escutar `DocumentOpened`, manter o `UIControlledApplication`, montar a
ribbon, criar o container DI, abrir transações com nomes apropriados,
converter exceções em `Result`. Sem um lugar dedicado, esse código se espalha
pelo `App.cs` e pelos comandos.

## Decisão
Criar `DarivaBIM.Revit.Hosting` com responsabilidade única: **hospedar** o
plugin no processo Revit. Conteúdo:
- `DependencyInjection/PluginHost` — wraps o `ServiceProvider` raiz.
- `Commands/RevitCommandContext` — `IRevitCommandContext` concreto.
- `Commands/RevitDocumentContext` — `IRevitDocumentContext` concreto.
- `Commands/RevitCommandExecutor` — wrapper padrão para todo `IExternalCommand`.
- `Events/RevitApplicationContext` — guarda a `UIControlledApplication`.
- `Ribbon/RibbonBuilder` — converte `RibbonDefinition` em ribbon Revit.
- (Futuro) `Events/ExternalEventQueue`, `Events/IdlingService`, hooks de
  `DocumentOpened/Closing/ViewActivated`.

Esse projeto **pode** depender de RevitAPI. Application e Domain nunca
dependem dele.

## Multi-versão
Hoje `Hosting` tem alvo único `net8.0-windows` + RevitAPI 2026 — basta para
V2025 e V2026. Quando V2023/V2024 forem materializados, faremos uma das duas:
1. `Hosting` vira multi-target (`net48;net8.0-windows;net10.0-windows`) com
   referência condicional à RevitAPI da versão.
2. Particionar em `Hosting.Common` (interfaces e código sem RevitAPI) +
   `Hosting.Vxxxx` por versão usando `SharedSource`.

A decisão final fica para o momento em que houver código real V2023/V2024
para evitar abstração prematura (ADR-0008).

## Consequências
- `App.cs` fica enxuto: monta DI, constrói ribbon, registra eventos.
- Comandos novos só precisam de `App.Executor.Execute(...)`.
- Smoke test com Revit testa o Hosting; testes unitários testam os UseCases
  com fakes.
