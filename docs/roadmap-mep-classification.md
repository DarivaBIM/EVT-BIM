# Roadmap de execução — Mep.Classification (MVP 1.B → 4)

> **Status:** 🟢 **Fases 1.B + 2.A FECHADAS e pushadas** (origin `2c9264a`, R4 **232/232** · 0/0 · 0/0 · 16/16). Próxima: **2.B-1** (POCOs de regra + loader). Codex panorâmico cadenciado no FIM da fase 2.B (cobre 2.A + 2.B).
> **Criado:** 2026-05-28 (Janela de Revisão, modo duas janelas). **Revisado:** **v1.3** (fase 1.B fechada) 2026-05-29.
> **Fonte de DESIGN:** `docs/mep-connection-rulebook.md` v2 (canônico). Este doc é a **EXECUÇÃO** — operacionaliza o §23 do rulebook em slices gateados.
> **Branch:** `claude/quantifica-followup-2026-05-27` @ `9dc82ff` (MVP 1.A fechado).

---

## Changelog v1.4 — decisão 2.B-2: `primaryAngleRule` sempre RAW (2026-06-01)

Gate da slice **2.B-2** (Janela de Revisão + **Codex Opção B**, 0 BLOCKER) cristalizou a semântica do `primaryAngleRule` no `pipe_connection_rules.json`:

- **Decisão:** `primaryAngleRule` é **sempre RAW** entre BasisZ outward (0..180) — **uma só semântica** p/ todos os BaseKinds. Antes misturava deflexão (elbow) e raw (resto), o que faria um joelho 45 real (raw 135) nunca casar `{40,50}` — armadilha de `if Elbow: 180−raw` esquecido na 2.B-3.
- **JSON ajustado:** `elbow-45` `{40,50}→{130,140}` (deflexão 45 = raw 135 ±5); `elbow-reducer` `{40,95}→{85,140}` (deflexão 45–90 = raw 90–135 ±5). `elbow-90` `{85,95}` e todos os `{175,185}` (tê/wye/luva/redução/válvula/cruzeta) **inalterados** (já raw; joelho 90 coincide porque raw 90 == deflexão 90).
- **Catálogo:** a deflexão (identidade "Joelho 45/90") continua em **`nominalAngleDeg`** (deflexão = 180 − raw) — separação limpa entre **filtro topológico** (raw) × **identidade de catálogo** (deflexão).
- **REQUISITO p/ 2.B-3 (refina a convenção de ângulo v1.2 / Codex #3):** o filtro compara o **raw DIRETO** da `AngleMatrix` contra `primaryAngleRule`, **inclusivo** (`>=`/`<=`), **sem** `if Elbow` e **sem** `180−raw`. A inversão raw→deflexão permanece **só** ao derivar `NominalAngleDeg` pro matching de catálogo — nunca no filtro topológico.
- **Teste-âncora:** `Elbow_primaryAngleRule_is_raw_not_deflection` trava `elbow-45 == {130,140}` + invariante "todo elbow tem `MinDeg >= 80`" (deflexão começaria em 40) → barra regressão silenciosa (protege o catálogo Tigre = dinheiro).
- ⚠️ **Follow-up levantado pela Janela de Código (NÃO resolvido aqui):** o `lateralAngleRule` do `wye` (`{40,50}`) tem o **mesmo cheiro de deflexão** do antigo `elbow-45`. Fora do escopo deste gate (a decisão Codex foi só sobre `primaryAngleRule`); a 2.B-3 precisa definir **como o ângulo lateral é extraído da matriz raw** antes de confiar nessa faixa. **Registrar p/ Codex/Revisão.**

---

## Changelog v1.1 — findings Codex incorporados (2026-05-28)

| # | Finding Codex | Ação no roadmap |
|---|---|---|
| C1 | **Ângulo:** não usar `abs(dot)` se precisa detectar 180° | Corrigido D1/1.B-1: matriz raw `Acos(clamp(dot,−1,1))` 0..180; `abs(dot)≈1` só p/ colinearidade do par passante. Resolve inconsistência rulebook linha 41 vs §9. |
| C2 | `ConnectorReading` precisa de mais contexto | +`OutwardNormal` (=BasisZ, não flow), `NativeIndex`, `IsConnected` (Domain-agnostic). |
| C3 | **Texto-only deve ser parcial/conservador** | D2/2.B-5: auto-aceita só token exclusivo+obrigatório; cap de confidence sem topologia; resto → `NeedsReview`/v1. |
| C4 | **v2 totalmente lazy** ou não há regressão zero | D4/3.B-2/4-1: toggle off → chama exatamente o `FindMatch` v1; nada de reader/rules/v2 carregados. +teste "v2 não tocado". |
| C5 | Falta slice do débito de polyfill | **Verificado desnecessário** (Janela de Revisão checou os `.csproj`): hosts do Adapter (`Adapters.V2025/V2026`) são `net8.0-windows`, `required`/`init` nativos. 1.B-0 **removido**. |
| C6 | `RevitPartTypeMapper` redundante com `PartTypeHints` (D7) | Removido de 1.B-2; Adapter só faz `PartType.ToString()`. |
| C7 | Falta `ElementTextsReader` no Adapter | Novo **1.B-3**. |
| C8 | `2.B-3` grande demais | Quebrado em **2.B-3 / 2.B-4 / 2.B-5**. |
| C9 | Migração: separar status, não forçar 872 identidades | 3.A-1: CSV com `status` (AutoAccepted/NeedsReview/Unclassified) + topN candidatos. |

Outros [OK] do Codex já refletidos como testes: ciclos de `inherits` + IDs únicos + "resource realmente embarcado" (2.B-1/2.B-2); golden tests de normalização compartilhados (2.A); `PartType` é **hint fraco** (D7); direção de dependência `Tigre→Mep` ok, `Mep→Tigre` proibida (D6).

---

## Changelog v1.3 — Codex panorâmico da fase 1.B (2026-05-29)

Codex de fecho de fase (rodou local, 207/207 + 16/16): **1 BLOCKER + 2 follow-ups**. O exame detalhado **promoveu** o concern do `ReductionKind` a BLOCKER. Fase 1.B **NÃO fechada** até a correção.

- 🔴 **BLOCKER — `ReductionKind`/`HasReduction` ignoram `DnEqualToleranceMm`:** uma luva DN 50/51 vira `BaseKind=Union` (tolerância aplicada na linha 97) **MAS** `ReductionKind=Concentric` + `HasReduction=true` (`Distinct()` exato) — contradição interna que poluiria a 2.B com feature `Reduced` falsa (toca o catálogo). **Fix:** `InferReduction` recebe `roles`+`opts` e agrupa DNs com a tolerância (`BranchOnly` só se RunA≈RunB ±tol e branch difere); `HasReduction` deriva de `ReductionKind != None`.
- 🟠 **Corrigir junto — filtro `Domain` fail-open:** se `connector.Domain` lança, hoje deixa passar; inconsistente com os outros 4 critérios (fail-closed). **Reverter pra fail-closed** (descarta + `DomainMismatch`). *(Revi minha instrução anterior — o Codex tem razão na consistência; o risco de "perder boca por erro" é o mesmo dos outros critérios, que já descartam.)*
- 🕓 **Follow-up (smoke Fase 4) — `count==4` roles arbitrários:** cruzeta simétrica pode pôr RunA/RunB em qualquer eixo (BaseKind=Cross OK; roles podem afetar redução). Baixo impacto MVP. Comentar no código + revisitar no smoke.
- 🕓 **Follow-up (smoke Fase 4) — `OutwardNormalGuard` ambíguo** em `dot≈0`/conector no centroide: geometria degenerada rara; revisitar com diagnostic/2ª heurística (tangente do tubo). Não inflar a assinatura do guard agora.
- ✅ **OK confirmado:** ângulo RAW/clamp/sem-abs, inline anti-paralelo, ordem determinística, guard antes do motor, conversão do Adapter, **Clean Architecture sem vazamento Revit no Domain**, R4 207/207 + 16/16.

---

## Changelog v1.2 — gate da slice 1.B-1 (2026-05-28)

Gate da Janela de Revisão (R4 verde validado: **Core 198/198**, V2025/V2026 0/0, zero avisos) + **Codex reativo** (tocava catálogo Tigre + divergia do rulebook §9) cristalizou a **convenção de ângulo do joelho**:

- **Convenção cristalizada (D1):** com `BasisZ` **OUTWARD**, `AngleMatrix` é o ângulo **raw** entre normais; a **deflexão** (o "ângulo" do catálogo: "Joelho 45/90") = **`180° − raw`**. Joelho 90° coincide (raw 90); **joelho 45° → raw 135**. O motor 1.B-1 entrega só o raw; a deflexão é derivada na 2.B. **Errata aplicada ao rulebook §9** (linha 450 dizia "retorna 45°" — fisicamente incorreto sob BasisZ outward).
- **Correção exigida na 1.B-1 antes do push:** o teste do joelho 45 usava fixture fisicamente de 135° rotulado "45" → trocar p/ joelho físico (normais a 135°, assert raw≈135) + documentar `deflexão = 180 − raw` no engine/Options. *(O motor de produção já estava correto — entrega raw; só o teste cristalizava a convenção errada.)*
- **Follow-up 1.B-2 (Codex #2):** blindar o sinal do `BasisZ` outward — validar via `dot(BasisZ, Origin − centroide) > 0` (ou pela tangente do tubo conectado); conteúdo Revit mal autorado pode reportar inward, o que inverteria a relação deflexão↔raw.
- **Follow-up 2.B (Codex #3):** expor `deflectionAngle` derivado explícito; o matching do catálogo usa `catalogAngle = 180 − raw`, nunca o raw cru.

---

## 0. Como usar este doc

- **Rulebook = o quê/porquê. Roadmap = em que ordem, com que gate.** Divergência semântica entre os dois → ganha o rulebook; abrir nota aqui. *(Exceção registrada: o ângulo da matriz é raw `Acos(dot)` 0..180 — vide C1; sugerir errata no rulebook §9/linha 41 ao implementar a 1.B.)*
- **Cada slice:** R4 verde + commit granular por path explícito + gate da **Janela de Revisão** antes de avançar. A **Janela de Código nunca mergeia/avança sozinha**.
- **Checkpoints:** `⟶ Codex` (avaliação externa obrigatória) e `⟶ GATE Matheus` (input humano insubstituível — smoke/review de catálogo/PR).
- **Tracker de status vivo:** §6. Atualizado ao fechar cada slice (junto da memória `project_evtbim_mvp_classification`).

---

## 1. Estado base — MVP 1.A fechado (não relitigar)

- 21 POCOs Domain em `Mep/Classification/{Ports,Connections}` + 26 tests headless (`518b1f6..9dc82ff`).
- Contratos-chave já existentes que as fases abaixo **consomem**:
  - `ConnectionTopology` (`PartType` string raw, `Ports[]`, `AngleMatrix`/`DistanceMatrix` jagged, `Inferred{BaseKind,Discipline,Category}`, `ReductionKind`, `IsInlinePairDetected`, helpers `AllDns`/`RunDn`/`BranchDn`/`HasReduction`).
  - `TopologyReadResult` (`Success` + `Topology?` + `Diagnostics[]`).
  - `MepPort` (`Role`, `DnMm`, `Direction`/`Origin` em `System.Numerics.Vector3`, `Shape`). `MepPort.Direction` carrega o **BasisZ outward** (não flow). XYZ→Vector3 é responsabilidade do Adapter.
  - `ConnectionIdentity`, `ClassificationConfidence`/`ConfidenceBucket`, enums sentinel-first.
  - Polyfills `IsExternalInit` + `RequiredMemberPolyfills` (`internal`) — **débito previsto NÃO se materializa**: o Adapter que constrói os records é `net8.0-windows` (`required`/`init` nativos); o polyfill internal do Domain só serve ao próprio Domain e já funciona. Revisitar só se um consumer `netstandard2.0` construir esses records.
- **R4 base (verificado 2026-05-28):** V2025 **0/0** · V2026 **0/0** · Core **159/159** · Architecture **16/16**.

---

## 2. Decisões arquiteturais duráveis (validadas pelo Codex 2026-05-28)

| # | Decisão | Status Codex | Racional / refinamento |
|---|---|---|---|
| **D1** | Motor §9 no **Domain pure** (`TopologyInferenceEngine` sobre `Vector3`); Adapter é casca. **Matriz de ângulos raw `Acos(clamp(dot,−1,1))` 0..180** (C1); `abs(dot)≈1` só p/ colinearidade do par passante. `ConnectorReading` carrega `OutwardNormal`+`NativeIndex`+`IsConnected` (C2). | [CONCERN] resolvido | `Connector` é selado/não-instanciável em xUnit; testar exige operar sobre `Vector3` extraído. |
| **D2** | Classifier com **modo "texto-only" conservador** (C3); migrador é runner C# que o reusa. | [CONCERN] resolvido | Catálogo é texto-puro. Auto-aceita só match lexical exclusivo+obrigatório; cap de confidence sem topologia; ambíguo → `NeedsReview`/v1. |
| **D3** | Loader = padrão `tigre_codes.json` (`EmbeddedResource` + `System.Text.Json`). | [OK] | +testes: schema version, IDs únicos, ciclo de `inherits`, overrides válidos, "resource embarcado". |
| **D4** | Toggle `UseV2ClassifierAsPrimary` **default `false`**; **v2 totalmente lazy** (C4). | [CONCERN] resolvido | Toggle off → chama exatamente `TigreCatalog.FindMatch`, sem reader/rules/v2. +teste "v2 não tocado". |
| **D5** | `LexicalNormalizer` próprio no Mep, sem tocar/depender de `TigreTextUtils`. | [OK] | +golden tests compartilhando exemplos de acento/case/dimensões. |
| **D6** | `TigreCatalogV2` em `Domain/Tigre/`. | [OK] | Direção: `Domain.Tigre` consome `ConnectionIdentity`; `Mep.Classification` **não** conhece catálogo Tigre. |
| **D7** | `PartType` string→`BaseKind` é `PartTypeHints` (Domain puro); Adapter só `ToString()`. | [OK] | Tratar como **hint fraco**, não verdade absoluta (famílias mal parametrizadas). |

---

## 3. Princípios de execução (propagar à Janela de Código)

1. **Clean Architecture:** `Mep.Classification` (Domain) sem `Autodesk.Revit.*`/`System.Windows.*`. `LayerIsolationTests` + `ForbiddenUsingsScanner` quebram o build se vazar. `Mep` **não** depende de `Domain.Tigre` (D6).
2. **R4 verde por commit:** build V2025 `.slnf` 0/0 · V2026 `.slnf` 0/0 · tests Core 100% · tests Architecture 100% (todos com `-p:SkipRevitDeploy=true`).
3. **records init-only**, file-scoped namespaces, **chaves obrigatórias em todo `if`**, nullable enabled, comentários WHY (sem acento no `.cs`, padrão do 1.A).
4. **`git add` por path explícito**. Conventional commits + `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`. Sem `--force`/`--no-verify`. **NÃO criar branch nova.**
5. **Tigre = `LicenseRequirement.Free`** permanente.
6. **Codex:** REATIVO antes de decisão espinhosa (feito neste checkpoint) + CADENCIADO ao **fim de cada fase** (1.B, 2.B, 3.A, 3.B, 4). Fases que tocam Tigre (3.A/3.B/4) = obrigatório.
7. **Gate humano (Matheus):** review do CSV (3.A-2), smoke E2E (4-2), PR pra `main` (4-3). Doc de smoke didático antes (feedback `smoke_orientation_didatic`).

---

## 4. Slices detalhados

Legenda: **Entra / NÃO entra · Arquivos · Depende de · R4 · Riscos · Codex · Done.**

### FASE 1.B — Leitura topológica

#### 1.B-0 · ~~Débito do polyfill~~ — **VERIFICADO DESNECESSÁRIO (removido)**
Hosts do Adapter (`DarivaBIM.Revit.Adapters.V2025/V2026`, que compilam o `SharedSource`) são **`net8.0-windows`** → `required`/`init` nativos. Quem constrói `MepPort`/`ConnectionTopology` é o engine **dentro do Domain** (polyfill internal já basta, mesmo assembly); o Adapter constrói só `ConnectorReading` em net8.0-windows (nativo). **Nenhuma ação.** Revisitar só se um consumer `netstandard2.0` (Abstractions/Infrastructure) passar a construir esses records.

#### 1.B-1 · `TopologyInferenceEngine` (Domain pure) — **o coração testável + 1ª slice**
- **Entra:** motor §9 puro sobre `Vector3`. **Matriz de ângulos raw** `angle[i,j]=Acos(clamp(Dot(ni,nj),−1,1))·180/π` (0..180) + matriz distância. **Inline-pair** = par com `angle≈180°` (anti-paralelo; BasisZ outward) **e** maior distância entre Origins (equivalente a `abs(dot)≈1` colinear; documentar robustez a orientação). Classificação `BaseKind` por contagem+ângulos (0=fail,1=Cap,2-inline=Union/Reducer,2-not-inline=Elbow c/ `NominalAngleDeg=round(angle/5)·5`,3-inline+lateral≈90=Tee,3-inline+lateral≈45=Wye,4=Cross,5+=MultiPort). Atribuição `PortRole` (tabela §6), `ReductionKind`, diagnostics geométricos (`InsufficientConnectorsAfterFilter`, `PartTypeMismatchInferred`, `PartTypeUndefined`). Input: `IReadOnlyList<ConnectorReading>` + (`partTypeRaw`, `Discipline`, `Category`). Output: `TopologyReadResult`. `TopologyInferenceOptions` (tolerâncias §9). `PartTypeHints` (D7, hint fraco). `ConnectorReading` (`OutwardNormal`/`Origin` Vector3, `DnMm`, `Shape`, `NativeIndex`, `IsConnected`).
- **NÃO entra:** nada de Revit/Connector; texto/lexical; regras JSON.
- **Arquivos (Domain):** `Connections/ConnectorReading.cs`, `Connections/TopologyInferenceEngine.cs`, `Connections/TopologyInferenceOptions.cs`, `Connections/PartTypeHints.cs`. Tests: `TopologyInferenceEngineTests.cs` + `PartTypeHintsTests.cs`.
- **Depende de:** 1.A (1.B-0 dispensado).
- **R4:** V2025/V2026 0/0; Core +~25 tests; Arch 16/16.
- **Riscos:** cobrir **todos** os casos §9 + 3 edge canônicos; determinismo de role (ordem estável via `NativeIndex`); epsilon/clamp em float; degenerate BasisZ; **não** colapsar 0°/180°.
- **Codex:** fim da fase 1.B.
- **Done:** engine classifica joelho 45/90, tê (PartType=Undefined inferido) e junção-rotulada-Tee→Wye em teste; R4 verde.

#### 1.B-2 · `ConnectionTopologyReader` (Adapter — casca fina)
- **Entra:** `ConnectorPhysicalFilter` (§9 passo 1: `ConnectorType==End`/`IsConnected`/`AllRefs`, `Shape==Round`, `Origin`/`BasisZ` válidos, `Radius>0`); `ConnectionTopologyReader` (orquestra: `MEPModel.ConnectorManager` → filtra → converte cada `Connector`→`ConnectorReading` via XYZ→`Vector3` + `RevitUnitConverter.FeetToMillimeters` + DN=`Radius*2` round + `BasisZ` normalizado em `OutwardNormal` → extrai `PartType.ToString()` + `Discipline`/`Category` da `BuiltInCategory` inline (sem "mapper" — D7/C6) → diagnostics de leitura → chama engine → merge diagnostics). `try/catch` **por conector** ao ler `Origin` (Revit 2025 lança p/ NonEndConn). Reusa o padrão de `RevitConnectorUtilities`. **Blindar o sinal outward do `BasisZ`** (Codex #2, v1.2): validar via `dot(BasisZ, Origin − centroide) > 0` (ou pela tangente do tubo conectado) e inverter se vier inward — conteúdo Revit mal autorado pode reportar inward, o que inverteria a relação deflexão↔raw que a 1.B-1 assume.
- **NÃO entra:** lógica geométrica (no engine); texto; regras; `RevitPartTypeMapper` (removido — C6).
- **Arquivos (Adapter):** `Common/Mep/ConnectorPhysicalFilter.cs`, `Common/Mep/ConnectionTopologyReader.cs`. **Sem unit test headless** (casca Revit-bound; exercitada no smoke Fase 4 — ausência deliberada, lógica testável está no engine).
- **Depende de:** 1.B-1.
- **R4:** V2025/V2026 0/0 (**atenção a APIs `Connector` 2025↔2026** — Codex avaliou risco baixo); Core/Arch inalterados.
- **Riscos:** `Connector.Origin` lança p/ NonEndConn em 2025 → try/catch por conector; conector lógico vs físico; `MEPModel` null; conversão raio→diâmetro→mm→round; **`BasisZ` inward em família mal autorada inverte a deflexão → blindar por sinal (Codex #2)**; cobertura só via smoke.
- **Codex:** fim da fase 1.B (panorâmico 1.B-0..1.B-3).
- **Done:** build 2025+2026 0/0; reader consumível; Codex 0 BLOCKER.

#### 1.B-3 · `ElementTextsReader` (Adapter — trivial)
- **Entra:** lê `FamilyName`/`TypeName`/`Description` reais do `Element` → `ElementTexts` POCO (Domain, definido na 2.B). Reusa `ElementDescriptionReader` existente. (Os pesos 3/2/1 são aplicados no **Classify**, não aqui.)
- **NÃO entra:** normalização/tokenização (é do `LexicalNormalizer` 2.A); scoring.
- **Arquivos (Adapter):** `Common/Mep/ElementTextsReader.cs`. Consumido só na 4-1 (wiring).
- **Depende de:** `ElementTexts` POCO (criado em 2.B-3). *Pode ser implementado junto da 1.B mas só ligado em 4-1.*
- **R4:** V2025/V2026 0/0.
- **Riscos:** parâmetros ausentes → string vazia (não null); `Description` via `ElementDescriptionReader` (instance→type fallback já validado).
- **Codex:** fim da fase 1.B.
- **Done:** reader devolve os 3 textos; R4 verde.

### FASE 2.A — Normalização lexical

#### 2.A-1 · `LexicalNormalizer` (Domain pure)
- **Entra:** `Tokenize(raw, opts)` (§10.1: boundary, accent strip, lower, split `_`/`.`/`/`/`x`/`×`, **camelCase split**, alias expand, negative tokens) + `TokenizerOptions` + constants `BaseKindTokens`/`TokenAliases`. Espelha técnica unicode de `TigreTextUtils` **sem tocá-lo/depender** (D5).
- **NÃO entra:** refactor de `TigreTextUtils`; regras JSON; `lexicalLines` (vem do JSON na 2.B).
- **Arquivos (Domain):** `Lexical/LexicalNormalizer.cs`, `Lexical/TokenizerOptions.cs`, `Lexical/BaseKindTokens.cs`, `Lexical/TokenAliases.cs`. Tests: `LexicalNormalizerTests.cs` + **golden tests** compartilhando exemplos acento/case/dimensões com os de `TigreTextUtils` (D5).
- **Depende de:** nada estrutural.
- **R4:** Core +~18 tests.
- **Riscos:** `te`/terminal (boundary), `sn`/snake (negative), acentos, `ESGRedux`→esg redux; **camelCase não pode estilhaçar siglas** (`PPR`/`PN`/`SR`/`SN`); duplicação consciente vs `TigreTextUtils` (D5).
- **Codex:** combinado com a fase 2.B.
- **Done:** tokeniza os edge cases do §10; golden tests verdes; R4 verde.

### FASE 2.B — Rulebook + Classify

#### 2.B-1 · POCOs de regra + loader
- **Entra:** POCOs `ConnectionRule`, `TopologyConstraint` (`partTypeAccepts`, `connectorCount`, `diameterRule`, primary/lateral angle rules, `inherits`/`overrides`), `DiameterConstraint`+`DiameterRelation`, `LexicalDisambiguator`, `RulebookTolerances`; `ConnectionRulebookLoader` (System.Text.Json + EmbeddedResource, D3) + resolução de `inherits` (deep-merge §12.1) + shortcut string→objeto (`diameterRule`, §12.2).
- **NÃO entra:** `Classify`; os dados das 32 rules (só fixture mínimo).
- **Arquivos (Domain):** `Connections/Rules/*.cs`, `Connections/Rules/ConnectionRulebookLoader.cs`; `.csproj` EmbeddedResource; tests do loader.
- **Depende de:** 1.A enums.
- **R4:** Core +tests.
- **Riscos:** deep-merge `inherits`/`overrides`; **detecção de ciclo de `inherits`** + **IDs únicos** + **"resource realmente embarcado"** (D3/Codex); System.Text.Json desserializando records `init`/`required` (confirmar polyfills 1.B-0 cobrem); shortcut string.
- **Codex:** fim da fase 2.B.
- **Done:** loader parseia fixture + resolve inherits + detecta ciclo; R4 verde.

#### 2.B-2 · `pipe_connection_rules.json` (dados — ~32 subtypes §18)
- **Entra:** JSON v2 completo (`baseKindTokens`, `tokenAliases`, `negativeTokens`, `tolerances`, ~32 rules §18 com topology/disambiguators/lexicalHints §19; `schemaVersion`). EmbeddedResource.
- **NÃO entra:** lógica.
- **Arquivos (Domain):** `Resources/pipe_connection_rules.json`; `.csproj` entry; tests de validação (parse 100%; todo subtype §18 presente; `inherits`/`promoteTo` apontam pra id existente; sem ciclo; todo `baseKind` ∈ enum; 9 BaseKinds cobertos; **NENHUM enum em sentinel — `relation != Unknown`, `ports` válidos, `baseKind != Unknown` — pega typo que o loader silenciaria** [gate 2.B-1]).
- **Depende de:** 2.B-1.
- **R4:** Core +validation tests.
- **Riscos:** **alta densidade de erro humano no JSON** → tests de validação são a rede; fidelidade ao §18/§19.
- **Codex:** fim da fase 2.B (revisa os dados explicitamente).
- **Done:** 32 subtypes carregam e validam; R4 verde.

#### 2.B-3 · `Classify` núcleo: filtro topológico + score lexical + confidence (§21 cam. 1–3,7)
- **Entra:** `ElementTexts` POCO; filtro de regras topologicamente compatíveis (cam. 1–2); score lexical híbrido (cam. 3; FamilyName×3+TypeName×2+Description×1 via `LexicalNormalizer`); `ConfidenceScore`/bucket (cam. 7, §16). Sem disambiguators ainda.
- **NÃO entra:** disambiguators/line/features (2.B-4); API pública/texto-only (2.B-5).
- **Arquivos (Domain):** `Connections/ConnectionRulebook.cs` (parcial), `Connections/ElementTexts.cs`, `Connections/ClassificationScoring.cs`; tests.
- **Depende de:** 1.A, 1.B-1, 2.A, 2.B-1/2.
- **R4:** Core +tests.
- **Riscos:** empate→ordem JSON; normalização de score lexical p/ 0..0.2; pesos. **CONVENÇÃO DE ÂNGULO (v1.4 — decisão 2.B-2, Codex Opção B):** o **filtro topológico** compara o **raw DIRETO** da `AngleMatrix` contra `primaryAngleRule` (sempre raw 0..180), comparação **inclusiva** (`>=`/`<=`), **sem** `if Elbow`. A inversão `deflexão = 180 − raw` permanece **só** na derivação de `NominalAngleDeg` (identidade p/ o **matching de catálogo**, Codex #3) — **nunca** no filtro topológico. ⚠️ `lateralAngleRule` (ex.: `wye {40,50}`) ainda não revisado sob raw — definir a extração do lateral da matriz antes de confiar na faixa.
- **Codex:** fim da fase 2.B.
- **Done:** filtra+scoreia candidatos canônicos; R4 verde.

#### 2.B-4 · `Classify`: disambiguators validados + linha + features (§21 cam. 4–6)
- **Entra:** disambiguators com validação topológica + `mandatoryLexical` (cam. 4); detecção de linha via `lexicalLines` (cam. 5); features flagged (cam. 6).
- **NÃO entra:** API pública/texto-only (2.B-5).
- **Arquivos (Domain):** `Connections/ConnectionRulebook.cs` (disambiguation/line/features); tests.
- **Depende de:** 2.B-3.
- **R4:** Core +tests.
- **Riscos:** promover só com topologia compatível + mandatory presente; ordem de precedência das features.
- **Codex:** fim da fase 2.B.
- **Done:** promove subtipo-filho corretamente; detecta linha/features; R4 verde.

#### 2.B-5 · `MepClassifier` API pública + **modo texto-only conservador (D2/C3)**
- **Entra:** `MepClassifier` (resolver por disciplina §13; cam. 8 monta `ConnectionIdentity`); **modo texto-only**: infere BaseKind por `baseKindTokens`, **auto-aceita só match lexical exclusivo com `mandatoryLexical`**; **cap de confidence sem topologia** (`High` só p/ subtipo inequívoco; pai-conhecido/filho-ambíguo → `Medium`/`NeedsReview`); casos geométricos sem token → `NeedsReview`/v1.
- **NÃO entra:** `TigreCatalogV2` (3.B); migrador (3.A).
- **Arquivos (Domain):** `Connections/MepClassifier.cs`; tests integração (full + texto-only conservador).
- **Depende de:** 2.B-4.
- **R4:** Core +tests integração.
- **Riscos:** **modo texto-only é a peça mais sutil** — calibrar cap p/ não inflar `NeedsReview` nem migrar lixo (C3).
- **Codex:** fim da fase 2.B (panorâmico 2.A + 2.B inteiro).
- **Done:** classifica casos full **e** texto-only conservador; R4 verde; Codex 0 BLOCKER.

### FASE 3.A — Migração do catálogo (offline) — **toca Tigre**

#### 3.A-1 · Migrador (exporter C#) — D2
- **Entra:** runner C# (console mínimo `tools/TigreCatalogMigrator/` **fora dos `.slnf` de produção**; ou gerador em `Core.Tests` com `[Trait]` manual) que carrega `tigre_codes.json` v1 → por SKU monta `ElementTexts(description)` → `MepClassifier` texto-only → emite `docs/tigre-catalog-migration-review.csv`. **Colunas incluem `status` (AutoAccepted/NeedsReview/Unclassified) + topN candidatos** (C9) além de sku/description/baseKind/geometryKind/angle/ports/features/line/confidence/reason. **Não força identidade completa** em SKU ambíguo.
- **NÃO entra:** tocar `tigre_codes.json` v1; gerar v2.
- **Arquivos:** `tools/TigreCatalogMigrator/*` (ou `Core.Tests/Tools/`); output `docs/*.csv`.
- **Depende de:** 2.B-5.
- **R4:** build de produção intacto (não acoplar o console aos `.slnf`).
- **Riscos:** depende 100% do texto-only conservador (D2); SKUs geométricos sem token → `NeedsReview`/`Unclassified` (esperado, não bug); volume 872.
- **Codex:** fim da fase 3.A (toca Tigre).
- **Done:** CSV com 872 rows + `status` + topN; distribuição de status reportada (não há cap silencioso).

#### 3.A-2 · Review humano + reimport — **⟶ GATE Matheus**
- **Entra:** Matheus revisa `NeedsReview`/`Unclassified`; migrador (modo reimport) lê CSV revisado → `tigre_codes_v2.json`. Validação §20.1 (**códigos preservados vs v1**; descrição idêntica; `Unclassified` permitido = sem baseKind, cai no v1 em runtime).
- **Arquivos (Domain):** `Tigre/tigre_codes_v2.json` (EmbeddedResource); tests de diff v1↔v2.
- **Depende de:** 3.A-1 + review humano.
- **R4:** Core +validation tests.
- **Riscos:** edição manual introduz erro (validação pega); encoding pt-BR do CSV.
- **Codex:** fim da fase 3.A. **GATE:** Matheus.
- **Done:** `tigre_codes_v2.json` validado; gate humano OK.

### FASE 3.B — Consumer Tigre — **toca o dinheiro**

#### 3.B-1 · `TigreCatalogV2`
- **Entra:** `TigreCatalogV2` em `Domain/Tigre/` (D6) — `FindMatch(ConnectionIdentity, …)` §22: pré-filtro estruturado (índice por `BaseKind` + GeometryKind/angle/Line/DNs/Features) + desempate bônus "Tigre: Descrição" + fallback score. `Domain.Tigre` consome `ConnectionIdentity`; **`Mep.Classification` não conhece Tigre** (D6).
- **NÃO entra:** Resolver/toggle (3.B-2); tocar v1.
- **Arquivos (Domain):** `Tigre/TigreCatalogV2.cs`; tests (espelha `TigreCatalogMatchingTests`).
- **Depende de:** 3.A-2, 2.B-5.
- **R4:** Core +tests.
- **Riscos:** contrato Tigre; `DnsCompatible`/`FeaturesCompatible`; desempate determinístico.
- **Codex:** fim da fase 3.B (obrigatório).
- **Done:** v2 casa os casos que v1 errava no smoke (joelho 45/90 paramétrico, tê vs junção); R4 verde.

#### 3.B-2 · `TigreCatalogResolver` + toggle (**lazy** — D4/C4)
- **Entra:** `TigreCatalogResolver` (§20.2) — fachada; toggle `UseV2ClassifierAsPrimary` **default false**. **Com toggle off, chama exatamente o `TigreCatalog.FindMatch` v1 — sem instanciar reader, sem carregar rules/v2.** Quando on: tenta v2, fallback v1.
- **NÃO entra:** wiring no pipeline (4-1); flip do default (4-2).
- **Arquivos (Domain):** `Tigre/TigreCatalogResolver.cs`; tests (off→só v1 com **fakes garantindo "v2 não foi tocado"**; on→v2; on+v2-miss→v1).
- **Depende de:** 3.B-1.
- **R4:** Core +tests.
- **Riscos:** qualquer eager-load de v2 com toggle off = regressão; precedência; fallback não pode regredir v1.
- **Codex:** fim da fase 3.B.
- **Done:** despacha certo nos 3 cenários; teste "v2 não tocado" verde; default v1; R4 verde.

### FASE 4 — Integração + smoke + PR

#### 4-1 · Wiring no Codificar Tigre (atrás do toggle **lazy**)
- **Entra:** trocar chamadas diretas a `TigreCatalog.FindMatch` em `TigreCodeScanner`/`TigreCodeApplier` por `TigreCatalogResolver.Resolve`. **Com toggle off, aceita o request antigo (lean/diameter/kindFilters) e não monta topologia/textos.** Só quando v2 ativo: injeta `ConnectionTopologyReader` (1.B-2) + `ElementTextsReader` (1.B-3) → `ConnectionIdentity`. Default v1 → comportamento idêntico.
- **NÃO entra:** flip do toggle (4-2); UI de confidence bucket (off-MVP §24).
- **Arquivos (Adapter):** `Features/TigreCodes/TigreCodeScanner.cs`, `TigreCodeApplier.cs` (+ wiring no ExternalEvent). Tests: toggle-off == baseline (regressão zero).
- **Depende de:** 1.B-2, 1.B-3, 3.B-2.
- **R4:** V2025/V2026 0/0; **toggle-off = regressão zero**.
- **Riscos:** pipeline pago direto; transação única; default off + lazy protegem.
- **Codex:** fim da fase 4 (panorâmico, toca Tigre).
- **Done:** toggle-off idêntico ao baseline; toggle-on produz v2 path; R4 verde.

#### 4-2 · Smoke E2E — **⟶ GATE Matheus** + flip
- **Entra:** doc `docs/smoke-mep-classification.md` didático (5 seções, feedback `smoke_orientation_didatic`); smoke do Matheus em modelo Tigre real (3 cenários §23: família paramétrica 45/90; tê vs junção; fittings custom `PartType=Undefined`); se passar → flip toggle default→v2.
- **Arquivos:** `docs/smoke-mep-classification.md`; flip do default no Resolver.
- **Depende de:** 4-1. **GATE:** Matheus.
- **Riscos:** v2 regride caso que v1 acertava → toggle volta a v1, vira hotfix.
- **Done:** 3 cenários ✓; toggle flipado; relatório na memória.

#### 4-3 · Codex final + PR — **⟶ GATE Matheus**
- **Entra:** Codex panorâmico do MVP completo (1.B→4); ajustes; PR pra `main`.
- **Depende de:** 4-2. **Codex:** panorâmico final obrigatório. **GATE:** Matheus decide quando abrir o PR.
- **Done:** 0 BLOCKER; PR aberto; memória "MVP fechado".

---

## 5. Calendário de gates

| Gate | Quando | Tipo | Status |
|---|---|---|---|
| **Checkpoint inicial** (D1–D7 + sequência) | antes de 1.B-1 | ⟶ Codex panorâmico | ✅ **0 BLOCKER (2026-05-28)** |
| Fim fase 1.B | após 1.B-3 | ⟶ Codex | ⬜ |
| Fim fase 2.B | após 2.B-5 (cobre 2.A+2.B) | ⟶ Codex | ⬜ |
| Fim fase 3.A | após 3.A-1 | ⟶ Codex (Tigre) | ⬜ |
| Review do CSV de migração | 3.A-2 | ⟶ GATE Matheus | ⬜ |
| Fim fase 3.B | após 3.B-2 | ⟶ Codex (Tigre) | ⬜ |
| Smoke E2E + flip | 4-2 | ⟶ GATE Matheus | ⬜ |
| Codex final + PR | 4-3 | ⟶ Codex + GATE Matheus | ⬜ |

---

## 6. Tracker de status

| Slice | Escopo curto | Status |
|---|---|---|
| **1.A** | POCOs Domain + tests | ✅ `9dc82ff` |
| **1.B-0** | ~~Polyfill~~ — verificado desnecessário (Adapter é net8.0-windows) | ➖ dispensado |
| **1.B-1** | `TopologyInferenceEngine` Domain (§9, ângulo raw) | ✅ `06eec06..020ea6d` |
| **1.B-2** | `ConnectionTopologyReader` + filtro + guard (Adapter) | ✅ `b42247f..591ab1f` + `0f76847` |
| **1.B-3** | `ElementTextsReader` (Adapter) | ✅ `057c4fa`,`4effbee` |
| **1.B fix** | BLOCKER `ReductionKind` + filtro Domain fail-closed + follow-ups | ✅ `511f0a6..3608a4b` |
| **2.A-1** | `LexicalNormalizer` + golden tests (D5: sem Tigre) | ✅ `858fa17`,`2c9264a` |
| **2.B-1** | POCOs regra + loader (ciclo/IDs/embedded) | ✅ `e44df97`,`97f7286` (local; push no fim da fase 2.B) |
| **2.B-2** | `pipe_connection_rules.json` — `primaryAngleRule` sempre RAW (Codex Opção B) | ✅ (local; push no fim da fase 2.B) |
| **2.B-3** | Classify: filtro + score + confidence | ⬜ |
| **2.B-4** | Classify: disambiguators + linha + features | ⬜ |
| **2.B-5** | `MepClassifier` API + modo texto-only conservador | ⬜ |
| **3.A-1** | Migrador → CSV (3-status + topN) | ⬜ |
| **3.A-2** | Review CSV + reimport → v2 json | ⬜ GATE |
| **3.B-1** | `TigreCatalogV2` | ⬜ |
| **3.B-2** | `TigreCatalogResolver` + toggle lazy | ⬜ |
| **4-1** | Wiring lazy atrás do toggle | ⬜ |
| **4-2** | Smoke E2E + flip | ⬜ GATE |
| **4-3** | Codex final + PR | ⬜ GATE |

---

## 7. Off-limits (rulebook §24 + CLAUDE.md)

❌ Não implementar perda de carga (`HydraulicLossKey`) · ❌ não tocar `TigreCatalog` v1, `TigreDetectionRules`, `tigre_codes.json` v1, `TigreTextUtils` · ❌ não cobrir HVAC/elétrica/gás · ❌ não persistir cache em disco · ❌ não criar GUID novo de shared param · ❌ não mexer em `RibbonCommandId`/`CommandRegistry` · ❌ não quebrar `LayerIsolationTests` (Mep sem Revit; Mep não conhece Tigre) · ❌ não `--force`/`--no-verify`/branch nova · ❌ não mergear pra `main` sem Codex + Matheus.
