# Codex Review Brief — Roadmap de execução Mep.Classification (MVP 1.B → 4)

**Você é um arquiteto/revisor sênior C#/.NET high-effort.** NÃO está revisando código — está revisando um **PLANO DE EXECUÇÃO** e **7 decisões arquiteturais duráveis** antes de uma única linha ser escrita. Objetivo: pegar erro de sequência, dependência faltante, e decisão durável errada **agora**, quando custa minutos, não depois de 6 fases implementadas.

Avalie contra dois eixos: **(A) a decomposição em slices** (ordem, dependências, granularidade, algo faltando) e **(B) as 7 decisões D1–D7**. Foque o esforço nas espinhosas: **D1, D2, D4, D5**.

---

## Contexto do projeto

- **EVT-BIM:** plugin Revit C# .NET 8 sob **contrato pago com a Tigre** (fabricante de tubos hidráulicos BR). **Não é SaaS** — qualidade máxima, toda feature `LicenseRequirement.Free`.
- **Clean Architecture estrita:** `Domain` (pure, `netstandard2.0`, **proibido** `Autodesk.Revit.*`/`System.Windows.*` — há `LayerIsolationTests` + `ForbiddenUsingsScanner` que quebram o build se vazar) → `Application` → `Adapters Revit` (`.SharedSource`, `net8.0`-ish multi-versão **Revit 2025 + 2026** via Shared Project) → `Plugin/WPF`.
- **R4 (gate de cada commit):** build V2025 `.slnf` 0/0 + build V2026 `.slnf` 0/0 + tests Core 100% + tests Architecture 100%.

## O que JÁ existe (MVP 1.A, fechado, não relitigar)

POCOs Domain em `Mep/Classification/{Ports,Connections}` (21 arquivos, 26 tests headless):
- `ConnectionTopology` (record): `PartType` (string raw), `Ports` (`IReadOnlyList<MepPort>`), `AngleMatrix`/`DistanceMatrix` (jagged `IReadOnlyList<IReadOnlyList<double>>`), `Inferred{BaseKind,Discipline,Category}`, `ReductionKind`, `IsInlinePairDetected`, helpers `AllDns`/`RunDn`/`BranchDn`/`HasReduction`.
- `TopologyReadResult` (record): `Success` + `Topology?` + `Diagnostics[]`.
- `MepPort` (record): `Role`, `DnMm` (int), `Direction`/`Origin` (**`System.Numerics.Vector3`**, não XYZ Revit), `Shape`. Conversão XYZ→Vector3 é do Adapter.
- Enums sentinel-first (`Unknown/None=0`): `Discipline`, `ProductCategory`, `BaseKind`, `GeometryKind`, `Feature[Flags]`, `ProductLine`, `Valve/Instrument/FilterKind`, `ReductionKind`, `Diagnostic*`. `ConnectionIdentity`, `ClassificationConfidence`/`ConfidenceBucket`.
- Polyfills `internal` `IsExternalInit` + `RequiredMemberPolyfills` (pra `record`/`init`/`required` em netstandard2.0). **Débito conhecido:** quando o Adapter (netstandard2.0) construir `new MepPort{...}`, o compilador exige esses atributos visíveis no consumer → resolver mirror/public na 1.B.

## Design canônico (resumo do rulebook v2, já aprovado)

Pipeline de classificação de peça MEP: **(0) Adapter** lê `Element`→`TopologyReadResult` (topologia + diagnostics); **(1)** valida topologia; **(2)** filtra regras topologicamente compatíveis; **(3)** score lexical híbrido (tokens manuais + derivados; FamilyName×3+TypeName×2+Description×1); **(4)** disambiguators validados (topologia + `mandatoryLexical`) promovem a subtipo-filho; **(5)** detecta linha (Redux/SN/Soldável/…); **(6)** features (flagged); **(7)** confidence float 0..1 + bucket; **(8)** monta `ConnectionIdentity`. Consumer **Códigos Tigre** filtra o catálogo (872 SKUs) por `ConnectionIdentity`. Inferência topológica (§9): matriz de ângulos (`Acos(abs(dot))`) + inline-pair (≈180° + maior distância) + classificação por contagem de conectores.

Fatos do código que fundamentam decisões:
- `DarivaBIM.Domain.csproj` **já** referencia `System.Text.Json 8.0.5` e **já** usa `EmbeddedResource`+`LogicalName` pra `tigre_codes.json` (carregado por `TigreCatalogJsonLoader`, com fallback embedded no Domain).
- `TigreTextUtils` (Domain/Tigre) **já** tem `Normalize` (FormKD), `NormalizeForSearch` (FormD), `Tokenize`, `StripDimensions` (strip DN/mm/polegada/PN), `ExtractPn`, `CoreTokens`. É usado pelo matcher v1.
- `TigreCatalog` v1 (`sealed class`, **Domain/Tigre/**, testado headless) — `FindMatch(leanText, diameterMmRound, IReadOnlyCollection<string>? kindFilters) → TigreCatalogEntry?`.
- `Autodesk.Revit.DB.Connector` é selado e não-instanciável em xUnit. Não há projeto de teste para o Adapter — só Core.Tests (Domain) + Architecture.Tests.

---

## (B) As 7 decisões a validar

| # | Decisão | Racional |
|---|---|---|
| **D1** ⚠️ | Motor de inferência geométrica do §9 vive no **Domain pure** (`TopologyInferenceEngine`, opera sobre `Vector3`), **não** dentro do `ConnectionTopologyReader` (Adapter). Adapter vira casca: extrai conectores → converte XYZ→Vector3 + feet→mm → chama o engine puro. | `Connector` é selado/não-instanciável em xUnit → testar a inferência exige operar sobre dados extraídos (`Vector3`), não sobre `Connector`. |
| **D2** ⚠️ | O classifier expõe um **modo "texto-only"** (infere `BaseKind` por `baseKindTokens`, camadas 3–6, sem filtro topológico). O **migrador de catálogo é um runner C#** que reusa esse modo (não um script Python reimplementando a heurística). | O catálogo Tigre é **texto-puro, sem geometria**; o classifier full exige `TopologyReadResult`. Python reimplementando → divergência garantida com produção. |
| **D3** ✅ | Loader do `pipe_connection_rules.json` = mesmo padrão do `tigre_codes.json` (`EmbeddedResource` + `System.Text.Json`). | Já existe no `.csproj`; não é decisão aberta. |
| **D4** ⚠️ | Toggle `UseV2ClassifierAsPrimary` **default `false` (v1)** até o smoke validar; flip pós-smoke. Wiring em `TigreCodeScanner`/`TigreCodeApplier`. | Pipeline pago; default seguro = regressão zero. |
| **D5** ⚠️ | `LexicalNormalizer` é **código próprio do módulo Mep**, espelhando a técnica unicode de `TigreTextUtils` **sem tocá-lo** nem dele depender. | Rulebook exige `Mep.Classification` **independente de `Domain.Tigre`**. Refatorar `TigreTextUtils` (usado pelo matcher v1 validado) = mexer no dinheiro sem necessidade. Aceita duplicação consciente (~15 linhas FormD). |
| **D6** | `TigreCatalogV2` vai no **`Domain/Tigre/`** (ao lado do v1), não em `Plugin/` (corrige rulebook §4). | v1 vive no Domain e é testado headless; v2 ao lado preserva testabilidade. |
| **D7** | Mapeamento `PartType` (string) → `BaseKind` hint é **função Domain pura** (`PartTypeHints`); o Adapter só faz `PartType.ToString()` (corrige rulebook §7). | Torna a tabela §7 testável headless; Adapter trivial. |

## (A) A sequência de slices

```
1.B-1  TopologyInferenceEngine (Domain pure §9) + tests headless          [dep: 1.A]
1.B-2  ConnectorPhysicalFilter + RevitPartTypeMapper + ConnectionTopologyReader (Adapter casca, sem unit test) [dep:1.B-1]
2.A-1  LexicalNormalizer + TokenizerOptions + BaseKindTokens/Aliases + tests [dep: —]
2.B-1  POCOs de regra (ConnectionRule/DiameterConstraint/Disambiguator) + loader (inherits/overrides) + tests [dep:1.A]
2.B-2  pipe_connection_rules.json (~32 subtypes §18) + tests de validação  [dep:2.B-1]
2.B-3  ConnectionRulebook.Classify + MepClassifier + MODO TEXTO-ONLY + tests integração [dep:1.A,1.B-1,2.A,2.B-1/2]
3.A-1  Migrador C# texto-only → docs/tigre-catalog-migration-review.csv     [dep:2.B-3]
3.A-2  [GATE humano] review do CSV + reimport → tigre_codes_v2.json + tests diff [dep:3.A-1]
3.B-1  TigreCatalogV2 (FindMatch por ConnectionIdentity, Domain/Tigre) + tests [dep:3.A-2,2.B-3]
3.B-2  TigreCatalogResolver (v2→v1 fallback, toggle default v1) + tests     [dep:3.B-1]
4-1    Wiring Resolver em TigreCodeScanner/Applier atrás do toggle (off=baseline) [dep:1.B-2,3.B-2]
4-2    [GATE humano] smoke E2E (3 cenários) + flip toggle→v2               [dep:4-1]
4-3    [GATE humano] Codex final + PR pra main                            [dep:4-2]
```

---

## Perguntas específicas (responda cada uma)

1. **D1:** extrair o motor §9 pro Domain é correto, ou há valor topológico (ex.: `Connector.AllRefs`, `Connector.MEPSystem`, ordem nativa de conectores) que **só existe no objeto Revit** e se perde ao reduzir tudo a `(Direction, Origin, DnMm, Shape)`? O `ConnectorReading` proposto é suficiente para os casos do §9, ou falta algum campo (ex.: `ConnectorType`, índice nativo, flow direction)?
2. **D2 (a mais sutil):** classificar 872 SKUs **sem geometria** via "modo texto-only" é sólido, ou perde demais (joelho 45 vs 90, tê vs junção dependem de ângulo, que o texto nem sempre carrega)? Como calibrar a confidence do modo texto-only para não inflar `needsReview` a ponto de tornar o gate humano inviável — nem suprimi-lo a ponto de migrar lixo? Há um caminho melhor (ex.: classificar parcialmente e deixar o resto pro matcher v1)?
3. **D4/wiring:** trocar `TigreCatalog.FindMatch` por `TigreCatalogResolver.Resolve` em `TigreCodeScanner`/`TigreCodeApplier` com default v1 garante **regressão zero**? Há risco de o caminho v2 (que exige `ConnectionTopologyReader` por elemento) introduzir custo/exceção mesmo com toggle off, se o wiring não for cuidadoso?
4. **D5:** duplicar a normalização unicode (em vez de promover um núcleo neutro compartilhado entre `TigreTextUtils` e `LexicalNormalizer`) é o trade-off certo, ou a dívida de divergência futura supera o risco de refatorar `TigreTextUtils`?
5. **Sequência:** alguma dependência invertida, slice grande demais pra um gate, ou peça faltando (ex.: onde o débito do polyfill `required` deve ser resolvido; falta um slice de `ElementTextsReader` no Adapter para alimentar o `Classify` com FamilyName/TypeName/Description reais do Revit)?
6. **Multi-versão:** algum risco específico de o `ConnectionTopologyReader` (1.B-2) divergir entre Revit 2025 e 2026 nas APIs de `Connector`/`PartType`/`ConnectorManager`?

## Formato de saída

Para cada decisão D1–D7 e para a sequência (A): **[OK]** / **[CONCERN]** / **[BLOCKER]** + 1–3 linhas de justificativa + correção sugerida quando não-OK. Liste explicitamente qualquer **slice/peça faltando**. Se nada for BLOCKER, diga "0 BLOCKER" no fim. Não invente problemas fora do escopo do plano.
