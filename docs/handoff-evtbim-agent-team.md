# EVT-BIM — Handoff pro Agent Team (pós Slice 3 — Codificar Tigre estendido)

**Gerado em:** 2026-05-26
**Pela:** janela de revisão (read-only), substituída pela Agent Team após este doc
**SHA-alvo na entrada:** `e2d9a9c` em `origin/claude/tigre-quantifica-recovery-2dedd5`
**Estado:** working tree clean, 0 commits ahead, build + tests verde, deploys frescos

---

## 0. O que você recebe

Plugin Revit C# .NET 8 sob **contrato pago da Tigre** (fabricante de tubos hidráulicos brasileiro). Cliente pagante de contrato fechado — não SaaS, não freemium. Todas features `LicenseRequirement.Free` permanente.

Você herda 2 features-âncora recém-fechadas em 2026-05-26:

- **Tigre Quantifica** — relatório de quantitativos do projeto Revit, agrupado por categoria/família/tipo/diâmetro/código, com auditoria estruturada (Tigre: Código ausente, Fabricante ausente, Sistema ausente, Cliente/Autor não preenchidos) + export CSV pt-BR
- **Codificar Tigre** (antes "Codificar Tubos") — aplica códigos de catálogo Tigre (~872 SKUs em 9 linhas) em pipes/fittings/accessories/fixtures filtrados por detector de marca

Repo canônico: github.com/DarivaBIM/EVT-BIM
Local lab PC: `C:\Dariva-Codes\EVT-BIM`

**Smoke test no Revit é gate humano (Matheus)** — pendente pra Slice 2 (validar correção do falso positivo Knauf/Amanco) e Slice 3 (codificar fittings catálogo em modelo real).

---

## 1. Estado técnico em `e2d9a9c`

### Branch ativa: `claude/tigre-quantifica-recovery-2dedd5`

17 commits desde main, em 2 macro-fases:

**Slice 2 (8 commits) — Catálogo + Detector + Auditoria correta:**
```
949a38c  Slice 1.5 R6 docs IQuantityScanService    ← baseline merge
2c9ddca  feat(catalog): polegadas pares + pn schema (2B.1)
42a9726  refactor(catalog): TigreCatalogEntry expõe ProductLine/Kind/Dn1/Dn2/Pn (2B.2)
49a4848  feat(catalog): FindMatch overload kindFilter (2B.3)
4332cef  feat(catalog): PN extraction desambigua PPR multi-PN (2B.4)
b357233  fix(pipe-codes): kindFilter="pipe" defensivo (2B.5)
4e2e84c  test(catalog): integração kindFilter + AmbiguityGuard + PN (2B.6)
3be2f79  feat(catalog): TigreCatalog.HasCode (2C.1)
8b8db75  feat(domain): TigreDetectionRules 6 sinais + veto Manufacturer (2C.2)
3d461b5  refactor(parameters): ElementDescriptionReader compartilhado (2C.3)
68f6843  feat(detector): TigreManufacturerDetector wrapper Revit (2C.4)
50f83cc  feat(scanner): audit Tigre: Código consome detector + cache TypeId (2D.1)
9419cc4  fix(quantifica): External event passa TigreCatalog pro Scanner (2D.2)
```

**Slice 3 (6 commits) — Codificar Tigre estendido:**
```
f5153bc  feat(collector): TigreElementCollector multi-category + filter detector (3.1)
dfefc91  feat(scanner): TigreCodeScanner generaliza multi-kind + DTO ampliado (3.2)
72537f9  feat(applier): dual-path write (instance→type→skip) + audit issues (3.3)
26626c8  feat(ribbon): rótulo "Codificar Tubos" vira "Codificar Tigre" (3.4)
9c8e1e9  feat(quantifica): UI subgroup por categoria + R4 final (3.5)
e2d9a9c  fix(catalog): kindFilter por conjunto + cleaner amplia + UI virtualization (3.6)
```

### Features registradas (6 botões na ribbon EVT-BIM)

1. **Biblioteca Tigre** — importador de famílias do catálogo
2. **Converter Tubos CAD** — converte representação CAD em pipes Revit
3. **Codificar Tigre** (ex "Codificar Tubos") — aplica código de catálogo em 4 categorias
4. **Tigre Quantifica** — relatório de quantitativos + auditoria + export
5. **Parâmetros em Lote** — preenchimento de parâmetros em massa
6. **Pontos de Utilização** — inserção de pontos hidráulicos

### Validação local em `e2d9a9c`

- `dotnet build DarivaBIM.V2025.slnf` → 0/0 erros/avisos, deploy auto OK
- `dotnet build DarivaBIM.V2026.slnf` → 0/0 erros/avisos, deploy auto OK
- `dotnet test DarivaBIM.Core.Tests` → 102/102
- `dotnet test DarivaBIM.Architecture.Tests` → 13/13 (LayerIsolation + ForbiddenUsingsScanner + RibbonWiringTests + outros)
- Deploys frescos em `%ProgramData%\Autodesk\Revit\Addins\{2025,2026}\EVT-BIM\` (timestamps 21:58)

### Estado do working tree

```
$ git status
On branch claude/tigre-quantifica-recovery-2dedd5
Your branch is up to date with 'origin/claude/tigre-quantifica-recovery-2dedd5'.
nothing to commit, working tree clean
```

Stash preservado: `stash@{0}: apagao-2026-05-24-pre-reconciliacao` (decisão Matheus pós-smoke — descartar vs cherry-pick refactor SharedParameters).

---

## 2. Decisões arquiteturais já validadas (NÃO relitigar)

Compromissos fechados pelas fases. Mudar exige justificativa MUITO forte.

### Cliente e modelo de cobrança

- Tigre é cliente pagante de contrato fechado
- TODAS features `LicenseRequirement.Free` permanente
- Não calibrar como MVP/freemium — não há premium tier planejado

### Arquitetura

- **Clean Architecture estrita** — Domain pure (sem RevitAPI/WPF) → Application (sem RevitAPI/WPF) → Adapters Revit → Plugin/Wpf
- **Multi-versão via Shared Project** com sufixo `.SharedSource` (não `.Shared`)
- **Sem DI container** — wiring manual com `new` direto no ExternalEvent
- **net8.0** no Core/Application, **net8.0-windows** no Plugin
- `LayerIsolationTests` + `ForbiddenUsingsScanner` em Architecture.Tests quebram build se Revit/WPF vazar pra Domain/Application

### Padrões C#

- C# 12, file-scoped namespaces
- Chaves obrigatórias (`IDE0011`) — primeira tentativa de write tool quebrou build por isso no DarivaBIM.AI vizinho
- Nullable enabled em todos os projetos
- `record struct` requer shim de IsExternalInit em netstandard2.0 — usa `struct` + IEquatable explícito quando precisar value semantics (Slice 2C decidiu isso pra TigreDetectionResult)
- `ElementId.Value` (long, cross-version 2024+), não `IntegerValue` (deprecated)
- `RevitCommandExecutor.Current!.Execute` wrap em todo IExternalCommand

### Tigre Quantifica

- CSV com BOM UTF-8 embutido, separador `;`, decimal pt-BR
- `QuantityCsvWriter` é pure function em Application/Services — testável headless
- ExternalEvent UM SÓ pra scan; export é síncrono no code-behind
- `ProjectInfoReader`: "(não preenchido)" + finding Yellow por campo; NUNCA `Environment.UserName`/`DateTime.Now`
- `MeasurementKind` por BuiltInCategory, NUNCA por LocationCurve
- Audit threshold "qualquer gap dispara finding" (Fabricante/Sistema sem percentual)
- **DTO aliases legados preservados** (`TigreScanResult.PipesTotal`/etc) — refator UI completo deliberadamente adiado pro Slice 4+

### Codificar Tigre / Catálogo

- `SharedParameterAccessor.GetParameter` — instance-only, escrita (decisão B2 do Slice 1.5)
- `SharedParameterAccessor.GetParameterIncludingType` — instance→type fallback, **read-only por design**
- **Dual-path applier:** instance → type → skip + `TigreApplyIssue` audit (3.3)
- **NÃO criar param Type novo programaticamente** em famílias custom IsTigre — usuário recebe `TigreApplyIssue` com mensagem clara ("Tigre: Código não disponível no instance nem no type da família X")
- Type write afeta TODAS instances do type — aceito por design (família catálogo = 1 SKU)
- `TigreCatalog` matcher usa `LeanDescription` (descrição sem marcadores dimensionais DN/mm/polegada/PN/comprimento)
- AmbiguityGuard: se múltiplas entries casam no tier, retorna null (não escolhe arbitrário)
- Strip de polegada cobre **9 variantes Unicode** (" ' ´ ' ' " " ′ ″) — Slice 2A.4
- PN extraction da query desambigua PPR multi-PN
- **`kindFilter` por conjunto** (`IReadOnlyCollection<string>`) — Slice 3.6 corrigiu mapping N:1 que tornava 55% catálogo invisível
- `TigreCodeCleaner` amplia pras 4 categorias (não só Pipes) — espelha Applier dual-path

### Detector "é Tigre?"

`TigreDetectionRules` é Domain pure (testável headless via TigreDetectionRulesTests, 17 fixtures). 6 sinais em ordem:

| Sinal | Lógica |
|---|---|
| 0 ExistingCodeMatch | código preenchido + bate catálogo via `TigreCatalog.HasCode()` → trumpa veto |
| 1 ManufacturerVeto | Manufacturer presente E NÃO casa Tigre/aliases → FALSE (trumpa positivos) |
| 2 ManufacturerTigre | token "tigre" exato em Manufacturer |
| 3 FamilyNameContainsTigre | token "tigre" exato em Family.Name |
| 4 DistinctiveBrandToken | AQUATHERM/ClicPEX/PPR/REDUX/TIGREFire/Soldável/Roscável/SR/SN — "tigre" NÃO entra aqui (Sinal 5 cobre) |
| 5 DescriptionMentionsTigre | token "tigre" exato em Description |

- Conservador: dúvida = FALSE
- Token "tigre" palavra exata (não substring) — evita falso positivo em "Petigreva"
- Wrapper `TigreManufacturerDetector` vive em `Adapters/Features/TigreQuantifica/` (cross-feature using consumido por TigreCodes — aceito, refator pra `Common/Detection/` está no backlog Slice 4+)

### Ribbon e wiring

- **3 pontos** pra adicionar botão Revit: enum `RibbonCommandId` + panel definition + `CommandRegistry`
- `RibbonWiringTests` guard pega gaps
- CommandIds internos PRESERVADOS mesmo quando label muda (RibbonCommandId.WritePipeCodes ficou interno, label virou "Codificar Tigre")
- File/folder names preservados (`PipeCodes*.cs`, pasta `PipeCodes/`) — git history + tests

### Workflow

- **R4 obrigatório** antes de cada commit: build V2025 + V2026 + tests Core + tests Architecture verdes
- Plugin deploy automático em `%ProgramData%\Autodesk\Revit\Addins\{2025,2026}\EVT-BIM\` via `<Exec>` post-build
- Deploy só completa com Revit fechado (DLL lock)
- Codex review pré-push em slices grandes ou decisões arquiteturais duráveis

---

## 3. Achados Codex reconciliados + débito técnico não-fixado

Codex (ChatGPT o3) revisou pré-push do Slice 3. Achou **1 BLOCKER demo-fatal** (kindFilter mismatch — 55% catálogo invisível) + 4 Risks/Nits + 3 perguntas extras. Blocker foi corrigido em Slice 3.6 (`e2d9a9c`). 7 itens foram pro backlog.

### Resolvido em 3.6 (`e2d9a9c`)

- **kindFilter por conjunto** (era único valor — mapping N:1 invisibilizava tee/elbow/reducer/cap/valve)
- **`TigreCodeCleaner` amplia 4 categorias** (era pipes-only — ficou incoerente com Apply ampliado)
- **`VirtualizingPanel.IsVirtualizingWhenGrouping=True`** nos 4 ListBox (perf 250+ grupos)

### Backlog do Codex — não bloqueiam Agent Team

1. **Nit** `TigreCodeWriter.Set()` ignora bool retorno (defensive — hoje só catch genérico). Revit normalmente lança exception em vez de retornar false, mas vale guard.
2. **Nit** Remover aliases legados `TigreScanResult.PipesTotal`/etc quando UI migrar completamente (estão lá pra evitar ripple massivo no XAML antigo)
3. **Nit** Mover `TigreManufacturerDetector` de `Features/TigreQuantifica/` pra `Common/Detection/` — detector é utility cross-feature por natureza. Refator simples (rename namespace + update usings em 2 lugares)
4. **Risk** Cache TypeId em `QuantityScanner`/`TigreElementCollector` quando instance Manufacturer/code override (caso edge raro mas possível em famílias custom)
5. **Nit** UI lista detalhes dos `TigreApplyIssue` (expandível em vez de só contagem) — hoje usuário sabe quantas falharam, não quais
6. **Nit** Re-bind shared param pra fittings/accessories/fixtures (decisão consciente do Slice 3 — revisita SE demanda real aparecer)
7. **Nit** Modeless travar Document no scan inicial — trocar de projeto entre scan e apply pode aplicar IDs no documento errado (raro, mas reproducível)

---

## 4. Backlog priorizado pro Agent Team

### Imediato — Slice 4 (UX da janela Tigre Quantifica — alta visibilidade)

Pedidos diretos do Matheus durante o smoke do Slice 1.5 (memória `project_evtbim_tigre` tem o histórico):

- **F1 ampliado:** botão "Corrigir agora" no audit Tigre: Código → abre Codificar Tigre pré-filtrado nos elementos do finding
- **F2:** click em finding na sidebar → seleciona elemento no Revit (`UIDocument.Selection.SetElementIds` + `ShowElements`) via ExternalEvent
- **F3:** filtro + busca na tabela (TextBox + `ICollectionView.Filter` — substring em Família/Tipo/Descrição/Código)
- **F4:** editar Cliente/Autor inline no header da janela (ExternalEvent → ProjectInfo update). Update inline da finding correspondente
- **F5:** collapse/expand por categoria via `Expander` + 5 mudanças visuais XAML do plano antigo do Slice 1.6 (header 3×2→2×3 com separador, legenda KPI, padding cards, IDENTIFICAÇÃO multi-linha, pill 14×14 + DropShadow)
- **F6-LITE:** leitura de "Tigre: Descrição" (sem GUID novo, sem mudança em PipeCodes — `PipeMetadataReader.GetTigreDescriptionOrNull()` com fallback type). +1 coluna no CSV. Audit Yellow se descrição faltando.

### Médio prazo — Slice 5+

- **Export Excel multi-aba padrão Tigre** via ClosedXML — Ficha (metadados) + HID-TIPO-PROJ (quantitativo principal agrupado por sistema com subtotais + Total Geral) + Resumo + Checklist NBR. Pasta de referência `C:\Users\mathe\OneDrive\01_Dariva-Plugin\00-Tigre\Tigre│Materiais Enviados\Tigre│Como são as entregas da EVT`
- **Validações NBR** (8160, 10844, 15575-6) — altura ponto utilização (LV/PIA/TLR/MLR + 0,20m se sem isometrias), prolongador caixa sifonada 0,5m, ventilação 1,2m. Inspiração no `EVT- CHECK LIST.xlsx`

### Longo prazo (pós-demo Tigre)

- Tabela de preços Tigre (preencher Preço Unit. + Total no Excel multi-aba)
- **PARTE 2** — ferramenta "Trocar Material de Tubos"
- DI singleton pra `TigreCatalogJsonLoader` (hoje 1 load por scan, ~159KB JSON re-lido)
- Refator detector pra `Common/Detection/` (item 3 do backlog Codex)
- Expansão de `TigreDetectionRules.KnownCompetitorManufacturers` (hoje 10 entries: Knauf/Amanco/Astra/Krona/Plastilit/Fortlev/Brasilit/Eternit/Preserve/Tubocon)

### Smoke tests pendentes (pré-requisito de qualquer Slice 4+)

Roteiro completo em `project_evtbim_tigre` memory. Resumo:

- **Slice 2** cenário B é o crítico — confirmar que famílias **Knauf/Amanco em PipeFitting** NÃO geram mais findings falsos "Tigre: Código" na sidebar
- **Slice 3** — Codificar Tigre estendido aplicando códigos em fittings catálogo Tigre em modelo real

---

## 5. Anti-scope-creep — NÃO entrar nessas direções

- **Re-bind global programático** do shared param Tigre: Código pra mais categorias — decisão consciente do Slice 3, confiar em type binding catálogo
- **Criar param Type novo programaticamente** em famílias custom — modelador prepara a família, plugin não altera schema
- **Renomear CommandIds internos** (`WritePipeCodes`, `OpenTigreQuantifica`, etc) — afeta wiring + RibbonWiringTests + git history
- **Outras categorias de catálogo** sem caso de uso real declarado pela Tigre — não adicionar OST_Sprinklers/etc sem demanda explícita
- **MVP/freemium pricing** — Tigre é contrato fechado, free permanente
- **Force-push** em `claude/tigre-quantifica-recovery-2dedd5` — branch compartilhada
- **Mexer no stash `apagao-2026-05-24-pre-reconciliacao`** sem decisão explícita do Matheus
- **Bypass de R4** — sempre roda V2025 + V2026 + tests Core + tests Architecture antes de commit
- **Bypass de Codex review** em slices grandes ou decisões arquiteturais duráveis (memória `workflow_codex_consult` define quando)

---

## 6. Pré-requisitos de ambiente

- **Revit 2025** + **Revit 2026** instalados (lab PC tem ambos; V2026.4 minimum)
- **.NET 8 SDK** pra build
- **Claude Code v2.1.x** com login no Max plan ativo
- **PowerShell 5.1+** ou Bash via Git Bash pra scripts de deploy
- **Git** + acesso ao repo `DarivaBIM/EVT-BIM`
- **Modelo Tigre real** — `26019-ESG CONVERTIDO-R00.rvt` ou similar pra smoke E2E. Pasta `C:\Users\mathe\OneDrive\01_Dariva-Plugin\00-Tigre\Tigre│Materiais Enviados\` tem RVTs reais
- **Tigre payload** — `C:\Users\mathe\AppData\Local\Temp\tigre_skus_payload_2026-05-26.txt` se for regenerar `tigre_codes.json`
- **Memória persistente** em `C:\Users\mathe\.claude\projects\C--Dariva-Codes-EVT-BIM\memory\`

---

## 7. Contexto mental herdado da Code Window (literal — copia coisas que só ela viu)

> 1. **Deploy bloqueado por Revit aberto é fricção normal.** O post-build `<Exec>` em `Plugin.V202{5,6}.csproj` chama `tools\deploy_revit_*.cmd` que copia DLL pra `%ProgramData%\Autodesk\Revit\Addins\`. Quando Matheus está smoke-testing, Revit segura o DLL, o `<Exec>` retorna exit 1, e o slnf full reporta erro mesmo com compilação verde. Solução: Matheus fecha Revit antes do R4 final. Não é regressão.
>
> 2. **TigreCatalogJsonLoader recarrega o catálogo a cada ExternalEvent.** Hoje cada PipeCodesScanExternalEvent / QuantityScanExternalEvent faz `new TigreCatalogJsonLoader().Load()`. JSON tem ~159KB — leitura é cheap mas vale virar DI singleton em slice futuro (`ITigreCatalogProvider` já existe como contrato, falta wiring singleton).
>
> 3. **Aliases legados não são por capricho.** `TigreScanResult.PipesTotal` é calculado como `=> ElementsTotal`. ViewModel atual + XAML continuam binding `PipesTotal` sem patch ripple. Slice 3 deliberadamente NÃO mexeu nisso pra evitar branch com 30+ arquivos patcheados. Slice 4 (que vai mexer em UI) é o lugar natural pra remover.
>
> 4. **Cross-feature using é intencional por enquanto.** `TigreElementCollector` (Features/TigreCodes/) importa `TigreManufacturerDetector` (Features/TigreQuantifica/). Detector é utility cross-feature. Codex sugeriu mover, revisão concordou que faz sentido, mas não no caminho crítico do Slice 3. Anotado pro Slice 4.
>
> 5. **Cache TypeId tem trade-off documentado.** `QuantityScanner.IsTigreCached()` e `TigreElementCollector.IsTigreCached()` cacheiam veredito por TypeId. Famílias Tigre catálogo têm Manufacturer/code no type, então cache funciona em 99.9% dos casos. Edge raro: instance Manufacturer override num PipeFitting que tem mais de uma instance. Decisão pragmática: cache puro sem bypass, refator se smoke detectar.
>
> 6. **Token "tigre" como palavra EXATA, não substring.** Em `TigreDetectionRules` (linhas 121-124), `mfgTokens.Contains("tigre")` exige token isolado. Comentário no código menciona "Petigreva" como exemplo de falso positivo evitado. Importante: lista de DistinctiveBrandTokens NÃO inclui "tigre" — Sinal 4 e Sinal 5 são separados pra que o veredito reporte a razão certa (DistinctiveBrandToken vs DescriptionMentionsTigre).
>
> 7. **6 entries do JSON ficam com `diameterMm=0`** (Ralo Linear Invisível 50/70/90cm + 2 conectores TIGREFire sem aspas). Filtro `r.DiameterMm > 0` no ctor de `TigreCatalog` exclui elas — comportamento intencional. Se Tigre liberar mais SKUs sem DN específico, o threshold de warning é 15% (test guard).
>
> 8. **Strip de polegada cobre aspas curvas Unicode** porque Tigre frequentemente copia descrições de Word/Excel pra catálogo, e isso traz `'`/`'`/`"`/`"`/`′`/`″`. Vide `TigreTextUtils.InchQuoteChars` (Slice 2A.4).
>
> 9. **Tigre tem 9 linhas de produto.** SR (Esgoto Série Reforçada), SN (Esgoto Série Normal), REDUX (Esgoto baixo ruído), Soldável (PVC água fria), Registros + Válvulas + Caixas, ClicPEX (PEX), AQUATHERM (CPVC água quente), TIGREFire (CPVC incêndio), PPR. 872 SKUs no total em `tigre_codes.json`.
>
> 10. **Memória system32 vs project memory.** Sessões antigas (revisão + code window) escreviam em `C--WINDOWS-system32\memory\` (sufixo do cwd C:\WINDOWS\system32). Agent Team escreve em `C--Dariva-Codes-EVT-BIM\memory\` (path NOVO, criado em 2026-05-26). 3 memórias semente criadas; resto fica como referência da janela system32.

---

## 8. Documentos canônicos no repo (LER ANTES de tocar código)

| Doc | Propósito |
|---|---|
| `CLAUDE.md` (raiz) | Auto-loaded por Claude Code. 6 princípios + ponteiros pros outros docs. |
| `README.md` (raiz) | Setup, build, deploy, smoke checklist. |
| `docs/handoff-evtbim-agent-team.md` | Este doc — handoff do Slice 3 pra Agent Team. |
| `src/docs/adr/ADR-0012-revit-adapter-feature-organization.md` | Padrão de organização Adapter por feature. |
| `src/docs/adr/ADR-0013-shared-parameters-architecture.md` | Arquitetura de shared parameters (instance vs type). |
| `src/docs/guides/adapter-anatomy.md` | Como adicionar nova feature seguindo o template canônico. |
| `src/docs/guides/shared-parameters-architecture.md` | Guia complementar do ADR-0013. |
| `src/docs/dynamo-migration/tigre-codes.md` | Histórico — referências históricas a `TigrePipeCollector` e `TigrePipeDataReader` (deletados em 3.3). Doc não atualizado, vale revisão de polish. |

### Memória persistente do Claude (lab PC)

`C:\Users\mathe\.claude\projects\C--Dariva-Codes-EVT-BIM\memory\MEMORY.md` é o índice. Entries semente criadas em 2026-05-26:

- `project_evtbim_tigre.md` — estado pós-Slice 3 + decisões + backlog priorizado
- `feedback_evtbim_autonomy_default.md` — política de autonomia (espelho do darivabim)

Memórias da janela system32 ainda relevantes mas não migradas (Agent Team pode replicar quando precisar):
- `feedback_tigre_detection_heuristic` — destilada no `project_evtbim_tigre` deste path
- `feedback_kindfilter_blind_spot` — lição do Slice 3.6, destilada idem
- `feedback_ribbon_wiring_checklist` — destilada idem
- `reference_tigre_materiais` — caminhos externos destilados idem
- `workflow_two_window_review` + `workflow_codex_consult` — padrões cross-projeto

---

## 9. Workflow esperado da Agent Team

- **Granular commits** por sub-slice (mesmo padrão das fases anteriores — Slice 2 teve 13 commits, Slice 3 teve 6)
- **Build + tests verde antes de cada commit.** Sem exceção. R4 obrigatório.
- **Não pusha antes de revisão validar SHA.** Lead do Agent Team aprova; humano Matheus aprova decisões arquiteturais
- **Push pra origin imediatamente após aprovação** — não acumular trabalho local (saga do `apagao-2026-05-24` provou o custo de não pushar)
- **Smoke local** (build + tests) após cada sub-slice. Smoke E2E (Revit + modelo Tigre real) só nos gates humanos (Matheus, antes de PR pra main)
- **Mantém memória persistente atualizada** — após cada slice fechado, atualizar `MEMORY.md` + arquivos de project memory específicos. Slice 4 vai fechar com 1-2 entries novas
- **Codex review pré-push** em slices que qualifiquem `workflow_codex_consult` (slice grande OU decisão arquitetural durável OU "cheiro" inarticulado de gap). Slice 4 inteiro provavelmente qualifica
- **Antes de codar feature com matching contra catálogo:** SEMPRE roda query no JSON do catálogo pra validar distribuição de discriminadores vs mapping do código. Lição do Slice 3.6 — N:1 mapping invisibilizou 55% do catálogo, salvo apenas pelo Codex

---

**PRONTO.** Esse é o input completo do Agent Team pro Slice 4+. Não envio mais nada após este turno.
