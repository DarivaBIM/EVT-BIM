# CLAUDE.md — Contexto canônico do EVT-BIM

> Auto-carregado por Claude Code (inclusive por teammates de Agent Teams).
> Mantenha enxuto — detalhes em `docs/*.md` e em memória persistente.

## O que é

Plugin Revit C# .NET 8 desenvolvido sob contrato pago para a **Tigre** (fabricante de tubos hidráulicos brasileiro). Clean Architecture estrita, multi-versão V2025 + V2026 via Shared Project. Repo: `C:\Dariva-Codes\EVT-BIM`. Remoto: github.com/DarivaBIM/EVT-BIM.

Cliente pagante de contrato fechado — **NÃO SaaS**. Todas as features são `LicenseRequirement.Free` permanente.

## Estado atual (pós Slice 3 — 2026-05-26)

- Branch ativa: `claude/tigre-quantifica-recovery-2dedd5` (em `origin`)
- HEAD: `e2d9a9c`
- **Tigre Quantifica** (Slices 1+1.5+2A+2B+2C+2D): relatório de quantitativos + auditoria + CSV pt-BR
- **Codificar Tigre** (Slice 3.1→3.6, antes "Codificar Tubos"): aplica códigos de catálogo Tigre em pipes/fittings/accessories/fixtures filtrados pelo detector (~872 SKUs em 9 linhas)
- 6 features ativas: Biblioteca Tigre, Converter Tubos CAD, Prolongadores, Parâmetros em Lote, Pontos de Utilização + as 2 acima
- Tests: Core 102/102, Architecture 13/13
- Builds V2025 + V2026 0/0 com deploys auto em `%ProgramData%\Autodesk\Revit\Addins\{2025,2026}\EVT-BIM\`
- **Smoke pendente:** Slice 2 + Slice 3 (gate humano Matheus)
- PR pra `main` em aberto — Matheus decide quando criar

## Stack

- .NET 8 + Revit 2025 + Revit 2026 (Windows)
- Solução: `DarivaBIM.sln` + slnfs `DarivaBIM.V2025.slnf` / `DarivaBIM.V2026.slnf`
- Clean Architecture: Domain pure → Application → Adapters Revit → Plugin/Wpf
- Multi-versão via Shared Project (`.SharedSource` suffix)
- `net8.0` no Core, `net8.0-windows` no Plugin

## Princípios não-negociáveis

1. **Core/Application sem dependência Revit/WPF** — `LayerIsolationTests` + `ForbiddenUsingsScanner` quebra build se vazar
2. **Wrapper `RevitCommandExecutor.Current!.Execute`** pra todo IExternalCommand
3. **Transação única por feature** no ExternalEvent — commit no caminho feliz; `tx.RollBack()` antes de qualquer return em erro
4. **3 pontos pra adicionar botão Revit**: enum `RibbonCommandId` + panel definition + `CommandRegistry`. `RibbonWiringTests` guard. CommandIds internos preservados mesmo quando label muda
5. **`LicenseRequirement.Free`** permanente em toda feature (Tigre é contrato fechado)
6. **R4 obrigatório** antes de cada commit: build V2025 + V2026 + tests Core + tests Architecture verdes. Plugin deploy auto via post-build `<Exec>` requer Revit fechado

## Convenções de código

- C# 12, file-scoped namespaces, chaves obrigatórias (`IDE0011`), nullable enabled
- Comentários WHY (não WHAT). Português quando esclarece intenção
- ViewModels SEM RevitAPI (LayerIsolation)
- `record struct` requer shim de IsExternalInit em netstandard2.0 — usa `struct` + IEquatable explícito quando precisar value semantics
- ElementId via `.Value` (long, cross-version 2024+), não `IntegerValue` (deprecated)
- Sem DI container — wiring manual (new direto no ExternalEvent)

## Padrões críticos da feature Codificar Tigre

- **Dual-path applier**: instance → type → skip + `TigreApplyIssue` audit (NUNCA cria param Type novo programaticamente)
- **`SharedParameterAccessor.GetParameter`** instance-only (escrita) vs **`GetParameterIncludingType`** (read-only por design)
- **`kindFilter` por conjunto** (`IReadOnlyCollection<string>`) — `OST_PipeFitting` cobre `{fitting, tee, elbow, reducer, cap}`. Lição do Slice 3.6 — N:1 mapping tornava 55% catálogo invisível
- **`TigreCatalog` matcher** usa `LeanDescription` (sem DN/mm/polegada/PN/comprimento), AmbiguityGuard retorna null em empate, PN extraction desambigua PPR
- **`TigreDetectionRules`** (Domain pure) com 6 sinais em ordem (ExistingCodeMatch trumpa veto Manufacturer; veto Manufacturer trumpa Family/Description). Token "tigre" como palavra exata, não substring

## Documentos canônicos

| Arquivo | Conteúdo |
|---|---|
| `README.md` (raiz) | Setup, build, deploy, smoke roteiro |
| `docs/handoff-evtbim-agent-team.md` | Handoff pós-Slice 3 pra Agent Team — input completo do time novo |
| `src/docs/adr/ADR-0012-revit-adapter-feature-organization.md` | Padrão de organização Adapter por feature |
| `src/docs/adr/ADR-0013-shared-parameters-architecture.md` | Arquitetura de shared parameters (instance vs type) |
| `src/docs/guides/adapter-anatomy.md` | Como adicionar nova feature seguindo o template canônico |
| `src/docs/guides/shared-parameters-architecture.md` | Guia complementar do ADR-0013 |

## Memória persistente relacionada

Diretório: `C:\Users\mathe\.claude\projects\C--Dariva-Codes-EVT-BIM\memory\`

- `project_evtbim_tigre` — estado pós-Slice 3 + decisões cristalizadas + backlog priorizado
- `feedback_evtbim_autonomy_default` — política de autonomia (espelho do darivabim-revit-ia)

## Operações sensíveis

- **Não mexer em CommandIds** sem revisão arquitetural (afeta wiring + RibbonWiringTests)
- **Não criar param Type novo programaticamente** em famílias custom — decisão do Slice 3, famílias precisam ser preparadas pelo modelador
- **Não re-bind global** do shared parameter Tigre: Código pra mais categorias (decisão do Slice 3 — confia em type binding catálogo)
- **Não force-push** em `claude/tigre-quantifica-recovery-2dedd5` — branch já compartilhada
- **Stash `apagao-2026-05-24-pre-reconciliacao`** (em `stash@{0}`) preservado — decisão Matheus pós-smoke (descartar vs cherry-pick refactor SharedParameters)

## Padrão de output preferido

- Português pt-BR
- Direto, sem hedging. "Fiz X" > "Eu poderia fazer X se você quiser"
- Toda mudança de código menciona arquivo:linha pra navegação
- Resumo final: 1-2 frases. O que mudou e o que falta
