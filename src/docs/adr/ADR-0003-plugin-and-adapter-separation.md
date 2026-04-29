# ADR-0003 — Separação Plugin × Adapter

## Status
Aceito.

## Contexto
Tanto o "ponto de entrada" quanto a "camada de tradução RevitAPI ↔ Domain"
podem usar a RevitAPI. É tentador colocar tudo num projeto só, mas isso mistura
responsabilidades muito diferentes:

- **Plugin**: ciclo de vida do .addin (`IExternalApplication`),
  `IExternalCommand`, ribbon, dockable panes, janelas WPF que rodam dentro do
  processo do Revit.
- **Adapter**: implementações concretas de contratos (`ITigreCodeApplyService`,
  `IRevitTransactionRunner`, `IPipeRepository`...) que traduzem chamadas da
  Application/Domain para a API do Revit.

## Decisão
Separar em dois projetos por versão:

- `DarivaBIM.Plugin.Vxxxx` — `App.cs`, `IExternalCommand`, ribbon,
  `CommandRegistry`, availability classes, `.addin` e recursos.
- `DarivaBIM.Revit.Adapters.Vxxxx` — `Repositories`, `Mapping`, `Adapters`,
  `Writers`, `Readers`, `Transactions`, `Selection`, `Filters`, `Parameters`,
  `Units`, `ExternalServices`.

Plugin **referencia** Adapter; Adapter **não** referencia Plugin (evita
dependência circular).

## ExternalServices coupled to UI
Eventos externos (`IExternalEventHandler`) que fecham o loop com janelas WPF
ficam no **Plugin**, não no Adapter, porque dependem das views. Adapters só
contêm lógica Revit que não conhece a UI. Isso preserva a regra "Adapter não
depende de Plugin".

## Consequências
- Eventuais "smoke tests" do adapter rodam abrindo um documento Revit; testes
  do plugin precisam de toda a UI WPF.
- IExternalCommand fica fino: recebe clique, resolve escopo de DI, chama
  UseCase e converte exceções em `Result.Failed/Cancelled` (ADR-0005).
