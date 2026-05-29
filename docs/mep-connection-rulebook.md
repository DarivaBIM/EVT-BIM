# MEP Connection Rulebook — design final v2

> **Status:** ✅ aprovado para implementação
> **Última revisão:** 2026-05-28
> **Histórico:** rascunho v1 (2026-05-27) → Codex review high-effort → 14 findings incorporados → v2 (2026-05-28)
> **Escopo:** módulo Domain reusable que classifica conexões MEP via topologia geométrica + texto. Independente de Códigos Tigre — também alimenta perda de carga, gerenciador de famílias, validação NBR, geração de isométricos.

---

## 1. Sumário executivo (1 página)

### Problema

O matcher atual do catálogo Tigre é **texto-puro**: compara `LeanCoreTokens` da entry vs `tokens(FamilyName + TypeName + Description)`. Smoke real em modelo Tigre revelou 3 falhas estruturais:

1. **Famílias paramétricas** como `ESG_Redux_Joelho 45_90` casam tanto com `Joelho 45 REDUX DN50` quanto com `Joelho 90 REDUX DN50` no catálogo → `AmbiguityGuard` retorna null → "Sem correspondência" mesmo a peça sendo facilmente identificável pela geometria.
2. **Tê vs Junção** indistinguíveis pelo texto quando a família usa mesma palavra — só o ângulo do conector lateral (90° vs 45°) diferencia.
3. **Famílias custom com `PartType=Undefined`** confundem todos os subtipos.

### Solução

Módulo Domain **`DarivaBIM.Domain.Mep.Classification`** (sem RevitAPI, reusable cross-feature) que classifica qualquer FamilyInstance MEP em uma **identidade canônica facetada**:

```
ConnectionIdentity {
  Discipline, Category, BaseKind, GeometryKind, NominalAngleDeg,
  Ports[], Features[], Line, Confidence, Reasons[]
}
```

Pipeline: **(1)** Adapter Revit extrai `TopologyReadResult` com diagnósticos → **(2)** Classifier filtra regras topologicamente compatíveis → **(3)** Score lexical híbrido (10% manual + 90% derivado do catálogo) com 3 fontes ponderadas (FamilyName×3 + TypeName×2 + Description×1) → **(4)** lexicalDisambiguators validados promovem pra subtipo-filho → **(5)** Linha (Redux/SN/Soldável/etc) detectada via dicionário auto-derivado.

### Decisões cristalizadas (após Codex review)

| Decisão | Status |
|---|---|
| Schema facetado (BaseKind + GeometryKind + Features + Line) em vez de Subtype string único | ✅ aceito (Codex BLOCKER #1) |
| `Ports[]` com `PortRole` em vez de `PrimaryDn`/`SecondaryDn` | ✅ aceito (Codex BLOCKER #2) |
| Usar `PartType` Revit granulares (Wye, LateralTee, MultiPort, ValveNormal) como sinal nativo | ✅ aceito (Codex HIGH #3) |
| `TopologyReadResult` com warnings/diagnostics em vez de só topology | ✅ aceito (Codex HIGH #4) |
| Ângulos via matriz robusta (inline = raw≈180 anti-paralelo + maior distância; abs(dot) só no ramal vs eixo) | ✅ aceito (Codex HIGH #5) — ⚠️ errata 2026-05-28: a matriz geral é raw `Acos(dot)` 0..180, **não** `abs` (vide §9 + roadmap C1) |
| `HydraulicLossKey` separado de `ConnectionIdentity` (perda de carga é outra coisa) | ✅ aceito (Codex HIGH #6) — fora do MVP |
| `Valve`/`Instrument` granular (shutoff/check/PRV/meter/instrument/filter) | ✅ aceito (Codex HIGH #7) |
| Rulebooks separados por disciplina (Pipe/Duct/Conduit/Gas) | ✅ aceito (Codex HIGH #8) — só Pipe no MVP |
| Tokenizer rigoroso (boundary + accent + negative tokens) | ✅ aceito (Codex MEDIUM #9) |
| Disambiguators com validação topológica + mandatoryLexical | ✅ aceito (Codex HIGH #10) |
| JSON `inherits` como objeto formal | ✅ aceito (Codex MEDIUM #11) |
| `diameterRule` como objeto port-based | ✅ aceito (Codex MEDIUM #12) |
| Cache híbrido (TypeId + geometricSignature + per-run, sem disk) | ✅ aceito (Codex MEDIUM #13) |
| Migração v1→v2 híbrida (script + CSV pra revisão) | ✅ aceito (Codex MEDIUM #14) |
| Confidence float 0..1 + reason codes interno; Low/Medium/High UI | ✅ aceito (Codex MEDIUM #15) |
| **lexicalHints híbrida** (10% manual + 90% derivada do catálogo) | ✅ decidido 2026-05-28 |

### Escopo MVP 1

Implementação focada em **PipeFitting hidráulico** (Tigre):
- 9 BaseKinds: Elbow, Tee, Wye, Cross, Union, Reducer, Cap, Valve, MultiPort
- ~25 subtypes mapeados (joelho 45/90, tê, tê-redução, junção simples/dupla, etc)
- Cobre Codificar Tigre + base pra futuras features

**Fora do MVP 1 (vai pro futuro):**
- `Mep.HydraulicLoss` (perda de carga via Crane TP-410 / Idelchik)
- `Mep.Classification.Fixture` (caixas sifonadas, ralos, corpo caixa)
- `Mep.Classification.Consumable` (anel borracha, abraçadeira, suportes)
- `DuctFittingRulebook` (HVAC)
- `ConduitFittingRulebook` (elétrica)
- `GasFittingRulebook`
- Outras disciplinas

---

## 2. Por que módulo Domain reusable

Hoje a lógica de classificação está acoplada à feature Códigos Tigre. Problema: outras ferramentas futuras precisam da mesma identificação:

| Feature futura | Consumo do `ConnectionIdentity` |
|---|---|
| **Perda de carga** | Tabela `K-loss[BaseKind, GeometryKind, AngleDeg, DnRatio, ValveKind, ValveOpening]` |
| **Gerenciador de famílias** | Agrupa famílias por `BaseKind` → linha → variantes (rosca, bucha, etc) |
| **Validações NBR** | Regras tipo "junção a cada 10m requer Tê de inspeção", "ramal > N° não pode usar curva curta" |
| **Geração de isométricos** | Símbolos por `BaseKind + GeometryKind` |
| **Quantitativos analíticos** | Agrupamento por `BaseKind + Line + dn1xdn2` |
| **Códigos Tigre** | Filtra catálogo Tigre por `(BaseKind, AngleDeg, ports[dn], Line)` |

Sem módulo Domain centralizado, cada feature duplica heurística → divergências sutis ao longo do tempo. Com módulo único, fix-once-correct-everywhere.

---

## 3. Histórico de decisões (rastreabilidade dos 15 findings Codex)

| # | Codex | Decisão | Onde no doc |
|---|---|---|---|
| BLOCKER#1 | Subtype facetado vs string | ACEITO | Seção 5 |
| BLOCKER#2 | Ports[] com Role | ACEITO | Seção 6 |
| HIGH#3 | PartType Revit granulares | ACEITO | Seção 7 |
| HIGH#4 | TopologyReadResult diagnostics | ACEITO | Seção 8 |
| HIGH#5 | Ângulos via matriz robusta | ACEITO | Seção 9 |
| HIGH#6 | HydraulicLossKey separado | ACEITO (fora MVP) | Seção 17 |
| HIGH#7 | Valve/Instrument granular | ACEITO | Seção 14 |
| HIGH#8 | Rulebooks por disciplina | ACEITO (só Pipe MVP) | Seção 13 |
| HIGH#9 | Tokenizer rigoroso | ACEITO | Seção 10.1 |
| HIGH#10 | Disambiguators validados | ACEITO | Seção 11 |
| MEDIUM#11 | JSON inherits objeto formal | ACEITO | Seção 12 |
| MEDIUM#12 | diameterRule port-based | ACEITO | Seção 12 |
| MEDIUM#13 | Cache híbrido | ACEITO | Seção 15 |
| MEDIUM#14 | Migração híbrida | ACEITO | Seção 20 |
| MEDIUM#15 | Confidence float+reasons | ACEITO | Seção 16 |
| — (Matheus) | lexicalHints híbrida 10/90 | ACEITO | Seção 10.2 |

---

## 4. Arquitetura de pastas e namespaces

```
src/Core/DarivaBIM.Domain/
  Mep/
    Classification/
      Ports/
        MepPort.cs                  POCO: PortRole + DnMm + Direction + Origin
        PortRole.cs                 enum: RunA, RunB, RunLarge, RunSmall, Branch,
                                          BranchLeft, BranchRight, Inspection,
                                          Inlet, Outlet, Unknown
      Connections/
        ConnectionTopology.cs       POCO: PartType + Ports[] + AngleMatrix +
                                          DistanceMatrix + InferredBaseKind +
                                          IsInlinePairDetected
        ConnectionIdentity.cs       POCO: Discipline + Category + BaseKind +
                                          GeometryKind + NominalAngleDeg + Ports[] +
                                          Features + Line + Confidence + Reasons[]
        BaseKind.cs                 enum: Elbow, Tee, Wye, Cross, Union, Reducer,
                                          Cap, Valve, MultiPort, Fixture, Unknown
        GeometryKind.cs             enum: ShortRadius, LongRadius, Offset, SShape,
                                          Straight, Branch, Multi, Unspecified
        Feature.cs                  [Flags] enum: ThreadedEnd, BrassBushing,
                                          Inspection, VisitCap, SlidingSleeve,
                                          Inverted, Reduced, BellAndSpigot,
                                          MaleEnd, FemaleEnd, FlangedEnd, SocketEnd
        ProductLine.cs              enum: Soldavel, Roscavel, Redux, SerieNormal,
                                          SerieReforcada, Aquatherm, ClicPEX, PPR,
                                          TigreFire, Unknown
        ConnectionRule.cs           POCO de uma regra do JSON
        ConnectionRulebook.cs       Carrega rules + Classify(topologyResult, texts)
        TopologyReadResult.cs       POCO: Success + Topology? + Diagnostics[]
        ClassificationConfidence.cs POCO: Score(0..1) + Bucket + Reasons[]
      Lexical/
        LexicalNormalizer.cs        boundary + accent + alias expand + negative
        BaseKindTokens.cs           manual constants
        TokenAliases.cs             manual constants
      Catalog/
        CatalogEntry.cs             POCO base (subclasses por fabricante)
        ProductIdentity.cs          POCO: BaseKind + Line + ports + features
      Resources/
        pipe_connection_rules.json  hidráulico (MVP 1)
        // duct_fitting_rules.json    HVAC (futuro)
        // conduit_fitting_rules.json elétrica (futuro)
        // gas_fitting_rules.json     gás (futuro)

src/Revit/DarivaBIM.Revit.Adapters.SharedSource/
  Common/
    Mep/
      ConnectionTopologyReader.cs   Element → TopologyReadResult
                                    via FamilyInstance.MEPModel.ConnectorManager
      RevitPartTypeMapper.cs        Revit PartType → canonical kind sinaliza
      ConnectorPhysicalFilter.cs    Filtra conectores físicos válidos (Codex H4)

src/Plugins/DarivaBIM.Plugin.SharedSource/
  Features/
    PipeCodes/
      TigreCatalogV2.cs             consome ConnectionIdentity + Tigre: Descrição bonus
      // (legado TigreCatalog mantido como fallback até v2 estável)
```

**Layer isolation:** `Mep.Classification` (Domain pure, sem RevitAPI) → `Adapter.Mep` (RevitAPI bridge) → `Plugin.Features.PipeCodes` (consumer). `LayerIsolationTests` + `ForbiddenUsingsScanner` guardam barreira.

---

## 5. `ConnectionIdentity` — schema facetado

Substitui o `Subtype: string` original. Cada faceta é **independente** e **ortogonal**:

```csharp
public sealed record ConnectionIdentity
{
    public required Discipline Discipline { get; init; }        // Plumbing | HVAC | Electrical | Gas
    public required ProductCategory Category { get; init; }     // PipeFitting | PipeAccessory | PlumbingFixture | Support | Consumable
    public required BaseKind BaseKind { get; init; }            // Elbow | Tee | Wye | Cross | Union | Reducer | Cap | Valve | MultiPort
    public GeometryKind GeometryKind { get; init; } = GeometryKind.Unspecified;
    public double? NominalAngleDeg { get; init; }               // 45 | 90 | 180 | null (pra Cap/Multiport)
    public required IReadOnlyList<MepPort> Ports { get; init; }
    public Feature Features { get; init; } = Feature.None;      // flagged enum
    public ProductLine Line { get; init; } = ProductLine.Unknown;
    public ClassificationConfidence Confidence { get; init; }   // Score + Bucket + Reasons
}
```

### Exemplo concreto: `ESG_Redux_Joelho 45_90` instance setada em 90°

```json
{
  "Discipline": "Plumbing",
  "Category": "PipeFitting",
  "BaseKind": "Elbow",
  "GeometryKind": "ShortRadius",
  "NominalAngleDeg": 90,
  "Ports": [
    { "Role": "RunA", "DnMm": 50, "Direction": [1,0,0], "Origin": [...] },
    { "Role": "RunB", "DnMm": 50, "Direction": [0,1,0], "Origin": [...] }
  ],
  "Features": "None",
  "Line": "Redux",
  "Confidence": {
    "Score": 0.92,
    "Bucket": "High",
    "Reasons": [
      "TopologyMatched:Elbow90",
      "PartTypeMatched:Elbow",
      "LexicalHint:joelho@familyName",
      "LineDetected:redux@familyName"
    ]
  }
}
```

### Exemplo: Tê de redução 100×100×50 SN

```json
{
  "BaseKind": "Tee",
  "GeometryKind": "Branch",
  "NominalAngleDeg": 90,
  "Ports": [
    { "Role": "RunA", "DnMm": 100, ... },
    { "Role": "RunB", "DnMm": 100, ... },
    { "Role": "Branch", "DnMm": 50, ... }
  ],
  "Features": "Reduced",
  "Line": "SerieNormal",
  "Confidence": { "Score": 0.88, "Bucket": "High", "Reasons": [...] }
}
```

---

## 6. `MepPort` — schema port-based

```csharp
public sealed record MepPort
{
    public required PortRole Role { get; init; }
    public required int DnMm { get; init; }       // diâmetro nominal arredondado
    public required XYZ Direction { get; init; }  // BasisZ outward
    public required XYZ Origin { get; init; }
    public ConnectorShape Shape { get; init; } = ConnectorShape.Round;  // Round | Rectangular | Oval
}

public enum PortRole
{
    Unknown,
    Inlet,           // genérico — inferido por direção do fluxo se disponível
    Outlet,          // genérico — única extremidade aberta (Cap)
    RunA,            // par inline principal (peças retas/joelhos/tês)
    RunB,            // par inline principal (peças retas/joelhos/tês)
    RunLarge,        // redutores assimétricos
    RunSmall,        // redutores assimétricos
    Branch,          // ramal (Tê 1 lateral, Junção 1 lateral)
    BranchLeft,      // Cruzeta / Junção dupla
    BranchRight,     // Cruzeta / Junção dupla
    Inspection       // Tê de inspeção (extremidade tampa removível)
}
```

### Convenções de Role assignment por BaseKind

| BaseKind | Ports |
|---|---|
| `Cap` | 1× `Outlet` |
| `Union`, `Reducer` (Ø igual) | 2× `RunA` + `RunB` |
| `Reducer` (Ø diferente) | `RunLarge` + `RunSmall` |
| `Elbow` (Ø igual) | 2× `RunA` + `RunB` |
| `Elbow` (Ø diferente — joelho de redução) | `RunLarge` + `RunSmall` |
| `Tee` (Ø simétrico) | 2× `RunA` + `RunB` + `Branch` |
| `Tee` (Ø assimétrico — redução) | `RunA` + `RunB` + `Branch` (DnBranch pode ≠ DnRun) |
| `Wye` (Junção simples) | `RunA` + `RunB` + `Branch` |
| `Cross` (cruzeta) | `RunA` + `RunB` + `BranchLeft` + `BranchRight` |
| `Cross` (junção dupla) | idem |
| `MultiPort` (manifold) | `Inlet` + múltiplos `Outlet` ou `Branch` |
| `Valve` | 2× `RunA` + `RunB` (extra `Inlet`/`Outlet` se direção inferível) |

### Helpers expostos no DTO

Para consumers (TigreCatalogV2, perda de carga, etc):

```csharp
public IReadOnlyList<int> AllDns => Ports.Select(p => p.DnMm).ToList();
public int? RunDn => Ports.FirstOrDefault(p => p.Role == PortRole.RunA)?.DnMm;
public int? BranchDn => Ports.FirstOrDefault(p => p.Role == PortRole.Branch)?.DnMm;
public bool HasReduction => Ports.Select(p => p.DnMm).Distinct().Count() > 1;
public ReductionKind ReductionKind { get; }  // None | Concentric | Eccentric | BranchOnly
```

---

## 7. PartType Revit mapping (Codex H#3)

O enum `Autodesk.Revit.DB.PartType` tem mais granularidade do que estávamos usando. Mapeamento canônico:

| Revit PartType | → Canonical BaseKind | GeometryKind / Notes |
|---|---|---|
| `Elbow` | `Elbow` | (decidido pelo ângulo da matriz) |
| `Tee` | `Tee` | lateralAngle ~90° → `Branch`, ~45° → reclassifica como `Wye` |
| `LateralTee` | `Tee` ou `Wye` | depende do ângulo lateral |
| `Wye` | `Wye` | (Revit nativo já reconhece) |
| `Cross` | `Cross` | lateralAngle ~90° → `Multi`, ~45° → junção-dupla |
| `LateralCross` | `Cross` | |
| `TapPerpendicular` | `Tee` | spud lateral 90° |
| `TapAdjustable` | `Tee` | spud lateral variável |
| `SpudPerpendicular`, `SpudAdjustable` | `Tee` | |
| `Union` | `Union` | |
| `Transition` | `Reducer` | (Ø diferente) ou `Union` (Ø igual com geometria diferente) |
| `Offset` | `Elbow` | `GeometryKind=Offset` (S-shape) |
| `MultiPort` | `MultiPort` | manifold/distribuidor |
| `Cap` | `Cap` | |
| `ValveNormal` | `Valve` | |
| `ValveBreaksInto` | `Valve` | |
| `InlineSensor`, `Sensor` | `Valve` | `Category=Instrument` |
| `PipeFlange` | `Union` | `Features.FlangedEnd` |
| `Other` / `Undefined` | (inferir via topologia) | exigir `ConnectionTopology.Infer()` por contagem+ângulos+DNs |

**Fallback se PartType inconsistente:** `ConnectionTopologyReader` ignora PartType nominal e usa **inferência por geometria** (count de conectores + ângulos + DNs) pra inferir BaseKind. Reasons[] anota `PartTypeUndefined:InferredFromGeometry`.

---

## 8. `TopologyReadResult` com diagnostics (Codex H#4)

Adapter Revit retorna estrutura rica com warnings, não só topology pura:

```csharp
public sealed record TopologyReadResult
{
    public bool Success { get; init; }
    public ConnectionTopology? Topology { get; init; }
    public IReadOnlyList<TopologyDiagnostic> Diagnostics { get; init; } = Array.Empty<TopologyDiagnostic>();
}

public sealed record TopologyDiagnostic
{
    public required TopologyDiagnosticCode Code { get; init; }
    public string Detail { get; init; } = "";
    public DiagnosticSeverity Severity { get; init; } = DiagnosticSeverity.Info;
}

public enum TopologyDiagnosticCode
{
    NoMepModel,                       // elemento não tem MEPModel
    NoConnectorManager,
    NonPhysicalConnectorSkipped,
    DomainMismatch,                   // conector Electrical num PipeFitting
    NonRoundConnectorIgnored,         // Shape ≠ Round em hidráulica
    MissingDiameter,                  // conector sem Radius/Width
    BasisZIncoherent,                 // BasisZ degenerado/zero
    OriginOutsideExpectedIntersection,
    NoConnectedPipes,                 // sem fluxo identificável
    PartTypeUndefined,                // exige inferência por geometria
    PartTypeMismatchInferred,         // PartType=Tee mas geometria sugere Wye
    InsufficientConnectorsAfterFilter // ex: PartType=Tee mas só 2 conectores físicos
}
```

Classifier consome diagnostics:
- **Severity=Error** → `Confidence.Score` máximo 0.3 (bucket=Low), aplicação automática desabilitada
- **Severity=Warning** → `Confidence.Score` penalizado em 0.2
- **Severity=Info** → não afeta score, só audit

---

## 9. Algoritmo de inferência topológica (Codex H#5)

Conectores Revit têm `BasisZ` apontando **outward** (saindo da peça). Pra inferir BaseKind de forma robusta:

```
1. FILTRO físico (via ConnectorPhysicalFilter):
   // ⚠️ ERRATA 2026-05-29 (slice 1.B-2, validado Codex): o estado de CONEXAO NAO
   // participa da existencia fisica da boca. Boca livre (ponta aberta) e boca
   // igual; filtrar por conexao transformaria um te com saida aberta em "2 bocas"
   // (=> Elbow/Union) e casaria codigo errado no catalogo (toca o dinheiro).
   - Connector.ConnectorType == End            (descarta Curve/logico)
   - Connector.Domain == DomainPiping           (boca de outra disciplina nao conta; senao DomainMismatch)
   - Connector.Shape == Round (em hidráulica)
   - Connector.Radius > 0
   - Connector.Origin não-null e não-degenerate
   - Connector.CoordinateSystem.BasisZ não-degenerate
   - ❌ NAO usar IsConnected / AllRefs.Size — descrevem conectividade do MODELO,
        nao a topologia intrinseca do fitting. (Codex 2026-05-29: "remover e seguro;
        manter e perigoso". AllRefs ainda mistura refs fisicas e logicas.)
   - follow-up (smoke Fase 4): se aparecer conector duplicado/espurio (tap/spud,
        familia mal feita), deduplicar por Origin+Eixo+Raio com tolerancia +
        Utility==false — mitigador GEOMETRICO, nunca de conexao.
   → conectores físicos válidos (lista filtrada)

2. CASOS TRIVIAIS:
   - count == 0: TopologyReadResult { Success=false, Diagnostics=[NoConnectorsAfterFilter] }
   - count == 1: BaseKind=Cap, port único=Outlet

3. MATRIZ DE ÂNGULOS (count ≥ 2):
   pra cada par (i, j) de conectores:
     dot[i,j] = Vector3.Dot(conn[i].BasisZ, conn[j].BasisZ)
     angle[i,j] = Acos(dot[i,j]) * 180/π  // 0° a 180°

   Convenção: peça reta tem BasisZ opostos (anti-paralelos), angle ~180°.
              peça angulada tem BasisZ formando ângulo < 180° entre si.

4. MATRIZ DE DISTÂNCIA:
   pra cada par (i, j):
     distance[i,j] = (conn[i].Origin - conn[j].Origin).Length

5. INLINE PAIR DETECTION (Tê/Junção/Cross):
   inline_pair = par (i,j) com angle[i,j] ∈ [175°, 185°]
                                          E distance[i,j] = MAX
   Se inline_pair existe → eixo principal definido.
   Else → não é peça "passante"; pode ser elbow ou multiport.

6. CLASSIFICAÇÃO:
   case count, inline_pair:

   count=2, inline:        BaseKind=Union ou Reducer (depende dn match)
                           GeometryKind=Straight
   count=2, NOT inline:    BaseKind=Elbow
                           // ERRATA 2026-05-28 (slice 1.B-1, validado Codex): com
                           // BasisZ OUTWARD, angle[i,j] raw = 180 - deflexao. O
                           // angulo do catalogo ("Joelho 45/90") e a DEFLEXAO:
                           //   NominalAngleDeg = 180 - round(angle[0,1] / 5) * 5
                           // joelho 90 coincide (raw 90); joelho 45 => raw 135.
                           // O motor 1.B-1 entrega a matriz RAW; a deflexao e
                           // derivada na 2.B.

   count=3, inline (i,j):  BaseKind=Tee ou Wye
                           Branch = conn[k] (k ≠ i, ≠ j)
                           branchAngle = angle entre conn[k].BasisZ e
                                         (eixo principal)
                           Se branchAngle ∈ [85°, 95°]: BaseKind=Tee
                           Se branchAngle ∈ [40°, 50°]: BaseKind=Wye

   count=3, NOT inline:    BaseKind=Multi, GeometryKind=Branch
                           (anomalia: tê sem inline detectado — verificar
                           se PartType nativo dá pista)

   count=4, inline:        BaseKind=Cross
                           Determinar se laterais ~90° (cruzeta clássica)
                           ou ~45° (junção dupla)
                           Set GeometryKind=Multi
                           Ports: RunA, RunB, BranchLeft, BranchRight

   count=5+:               BaseKind=MultiPort, GeometryKind=Multi
                           (manifold, distribuidor, etc)

7. PORT ROLE ASSIGNMENT:
   pra cada conector → atribui PortRole conforme convenção (tabela seção 6)

8. RESULT:
   TopologyReadResult { Success=true, Topology={BaseKind, Ports, AngleMatrix,
                                                 DistanceMatrix, InferredBaseKind},
                        Diagnostics=[...info codes...] }
```

**Edge cases handled:**
- **Joelho 45_90 instance setada em 45°**: matrix de ângulos retorna **135°** (raw entre BasisZ outward), BaseKind=Elbow; deflexão = 180−135 = **45°** → matcher catálogo bate `Joelho 45 REDUX DN50`, não ambíguo. ⚠️ **ERRATA 2026-05-28** (slice 1.B-1, validado Codex): a redação v2 dizia "retorna 45°", **fisicamente incorreto** sob BasisZ outward. A matriz é raw; `NominalAngleDeg` (deflexão) = `180 − raw`.
- **Tê com PartType=Undefined**: contagem=3 + inline detectado + lateral ~90° → BaseKind=Tee inferido (Reasons["PartTypeUndefined:InferredFromGeometry"]).
- **Junção 45° rotulada como Tee pelo modelador**: PartType=Tee mas geometria → lateral 45° → BaseKind=Wye (Diagnostic PartTypeMismatchInferred).

---

## 10. lexicalHints híbrida (10% manual + 90% derivada)

### 10.1 Tokenizer rigoroso (Codex H#9)

```csharp
public static class LexicalNormalizer
{
    public static IReadOnlyList<string> Tokenize(string raw, TokenizerOptions opts);
}

public sealed class TokenizerOptions
{
    public bool StripAccents { get; init; } = true;        // Soldável → soldavel
    public bool ToLowerInvariant { get; init; } = true;
    public bool SplitOnUnderscoreAndDot { get; init; } = true;
    public bool SplitCamelCase { get; init; } = true;       // ESGRedux → esg redux
    public bool RequireBoundary { get; init; } = true;      // "te" só casa como token, não dentro de "terminal"
    public bool ExpandAliases { get; init; } = true;
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? Aliases { get; init; }
    public IReadOnlyDictionary<string, IReadOnlyList<string>>? NegativeTokens { get; init; }
}
```

Boundary: regex split em `[\s_\-\.\/×x×]+` + word boundaries. Match `"te"` SÓ como token isolado, não substring em "terminal".

Negative tokens: se `"terminal"` aparece no mesmo texto que ativaria `"te"`, suprime o match `"te"` (anti-falso-positivo).

### 10.2 Estratégia híbrida (decidida 2026-05-28)

**Manual (~55 entries no JSON do rulebook):**

```json
"baseKindTokens": {
  "elbow":      ["joelho", "curva"],
  "tee":        ["te", "tê"],
  "wye":        ["juncao", "wye"],
  "union":      ["luva", "uniao"],
  "reducer":    ["reducao", "bucha", "redutor"],
  "cap":        ["cap", "tampao", "plug"],
  "valve":      ["registro", "valvula", "esfera", "gaveta", "retencao"],
  "cross":      ["cruzeta", "cross"],
  "multiport":  ["manifold", "barrilete", "distribuidor", "coletor"]
},

"tokenAliases": {
  "te":      ["tê", "tee"],
  "juncao":  ["junção", "wye", "lateral"],
  "reducao": ["redução", "redutor"],
  "curva":   ["bend", "curve"],
  "uniao":   ["união", "coupling"],
  "joelho":  ["elbow", "cotovelo"]
},

"negativeTokens": {
  "te":  ["terminal", "tempo", "teflon"],
  "sn":  ["snake"],
  "sr":  ["sra"]
}
```

**Auto-derivado pelo script `tools/derive_lexical_hints.py`:**

```python
def derive_hints(entry: dict, base_kind_tokens: dict, aliases: dict) -> dict:
    """
    entry = {
      "description": "Joelho 45 REDUX DN50",
      "subtype_suggested": "elbow",
      "productLine": "REDUX",
      ...
    }

    Pipeline:
    1. Tokenize description com TokenizerOptions
    2. Remove regex (\\d+|dn\\d+|pn\\d+|mm) — números/dimensões
    3. Subtract baseKindTokens[entry.subtype]  — remove genéricos
    4. Expand via aliases
    5. Cross-reference com productLine field
    → entry.autoDerivedHints = ["redux"]
    """
```

**lexicalLines** derivado automaticamente do field `productLine` do catálogo:

```python
def derive_lexical_lines(catalog: list[dict]) -> dict:
    """
    Para cada productLine único no catálogo, gera entry no dicionário:
      productLine="REDUX" → {"Redux": ["redux"]}
      productLine="SN" → {"SerieNormal": ["sn", "serie normal", "esg sn"]}
    """
```

### 10.3 Score lexical com hints híbridos

No classifier, para cada candidate (regra topologicamente compatível):

```
effectiveHints =
    rulebook.baseKindTokens[candidate.baseKind]
    UNION
    catalogEntry.autoDerivedHints           // se classifier também usa catálogo
    UNION
    candidate.manualExtraHints              // se a regra declara hints extras

effectiveHints = expandAliases(effectiveHints, rulebook.tokenAliases)
effectiveHints = removeNegatives(effectiveHints, allText, rulebook.negativeTokens)

score = 0
for hint in effectiveHints:
  if hint in NormalizeForSearch(familyName):   score += 3
  if hint in NormalizeForSearch(typeName):     score += 2
  if hint in NormalizeForSearch(description):  score += 1
```

### 10.4 Custo manutenção (vs alternativas)

| Cenário | Manual puro | Híbrido |
|---|---|---|
| Setup inicial | ~3h JSON | ~2h (1h manual + 1h script) |
| Adicionar fabricante (200 SKUs) | ~2h | **zero** |
| Adicionar BaseKind novo | ~10min | ~5min |
| Falso positivo em smoke | 5min | 5min (negativeTokens) |

---

## 11. lexicalDisambiguators com validação topológica (Codex H#10)

Promove o match pra subtipo-filho **SÓ se**:
1. Filho é **topologicamente compatível** com topology atual
2. Filho declarado tokens `mandatoryLexical` estão presentes (se houver)

```json
"elbow-90": {
  "lexicalDisambiguators": [
    {
      "trigger": "rosca",
      "promoteTo": "elbow-90-threaded",
      "mandatoryLexical": ["rosca"],
      "topologyMustMatch": true
    },
    {
      "trigger": "bucha",
      "promoteTo": "elbow-90-brass-bushing",
      "mandatoryLexical": ["bucha", "latao"],
      "topologyMustMatch": true
    },
    {
      "trigger": "transposicao",
      "promoteTo": "transposition-curve",
      "mandatoryLexical": ["transposicao"],
      "topologyMustMatch": true
    }
  ]
}
```

Lógica:
```
para cada disambiguator do winner:
  se disambiguator.trigger ∈ allText:
    candidate_child = rules[disambiguator.promoteTo]
    se candidate_child.topology.compatibleWith(currentTopology):
      se all tokens mandatoryLexical ∈ allText:
        winner = candidate_child
        Reasons.add("DisambiguatorPromoted:{trigger}→{promoteTo}")
        break
```

Evita falso-positivos como `"curva"` numa peça que tecnicamente é joelho.

---

## 12. JSON schema formal (Codex M#11 + M#12)

### 12.1 `topology.inherits` como objeto

Antes (frágil):
```json
"juncao-reducao": {
  "topology": "inherits juncao-simples, diameterRule=lateralDifferentFromInline"
}
```

Depois (formal):
```json
"juncao-reducao": {
  "topology": {
    "inherits": "juncao-simples",
    "overrides": {
      "diameterRule": { "mode": "roles", "constraints": [
        { "ports": ["RunA", "RunB"], "relation": "equal" },
        { "ports": ["Branch"], "relation": "lessThan", "target": "RunA" }
      ]}
    }
  }
}
```

Vantagens: validação por JSON Schema, diff legível, IDE autocomplete, refactoring seguro.

### 12.2 `diameterRule` port-based extensível

Em vez de enum string limitado (`"equal"`, `"different"`, `"anyMatchingTwoEqual"`):

```json
"diameterRule": {
  "mode": "roles",
  "constraints": [
    { "ports": ["RunA", "RunB"], "relation": "equal" },
    { "ports": ["Branch"], "relation": "lessOrEqualThan", "target": "RunA" }
  ]
}
```

`relation` enum:
- `equal` (±tolerance)
- `different`
- `lessThan`, `lessOrEqualThan`, `greaterThan`, `greaterOrEqualThan`
- `single` (só 1 port)
- `any` (não constraint)

`target` pode ser `PortRole` ou valor numérico fixo.

API interna sempre é objeto. JSON pode aceitar string como **shortcut** (`"diameterRule": "equal"` expande pra forma canônica internamente).

---

## 13. Rulebooks por disciplina (Codex H#8)

Cada disciplina tem JSON próprio + classe Rulebook:

```
PipeConnectionRulebook       hidráulico (MVP 1)
DuctFittingRulebook          HVAC (futuro)
ConduitFittingRulebook       elétrica (futuro)
GasFittingRulebook           gás (futuro)
PlumbingFixtureRulebook      caixas/ralos/peças hidrossanitárias (futuro)
```

Cada um carrega seu `<discipline>_rules.json` separado, mas todos consomem o mesmo módulo `Mep.Classification` core (`ConnectionTopology`, `MepPort`, `LexicalNormalizer`, etc).

Resolution por disciplina:
```csharp
public sealed class MepClassifier
{
    private readonly IReadOnlyDictionary<Discipline, IConnectionRulebook> _rulebooks;

    public ConnectionIdentity? Classify(TopologyReadResult result, ElementTexts texts)
    {
        var discipline = result.Topology?.InferredDiscipline ?? Discipline.Unknown;
        if (_rulebooks.TryGetValue(discipline, out var rulebook))
            return rulebook.Classify(result, texts);
        return null;
    }
}
```

**MVP 1 implementa só `PipeConnectionRulebook`.** Outras disciplinas adicionadas conforme demanda.

---

## 14. Granulação Valve/Instrument (Codex H#7)

`BaseKind=Valve` é genérico demais. Refino em sub-enums:

```csharp
public enum ValveKind
{
    Unknown,
    Shutoff,            // esfera, gaveta, globo (corte de fluxo)
    Check,              // retenção (válvula unidirecional)
    PressureReducing,   // VRP
    Flush,              // descarga (vaso sanitário, urinol)
    Relief,             // alívio de pressão
    Butterfly,          // borboleta
    Ball,               // esfera específica
    Gate                // gaveta específica
}

public enum InstrumentKind
{
    Unknown,
    PressureMeter,      // manômetro
    FlowMeter,          // hidrômetro
    PressureSensor,
    TemperatureSensor,
    FlowSensor
}

public enum FilterKind
{
    Unknown,
    YStrainer,          // filtro Y
    InlineFilter,
    BasketFilter
}
```

`ConnectionIdentity` ganha campos opcionais conforme `BaseKind`:

```csharp
public ValveKind? ValveKind { get; init; }            // só se BaseKind=Valve
public InstrumentKind? InstrumentKind { get; init; }  // só se Category=Instrument
public FilterKind? FilterKind { get; init; }
```

Detectado via `lexicalHints` específicos em `valve-shutoff`, `valve-check`, `meter`, `instrument`, `filter` (sub-rules no JSON).

---

## 15. Cache híbrido (Codex M#13)

**Per-run, in-memory.** Não persistir em disco (overengineering pro tamanho do problema).

Chave de cache:
```
CacheKey = (
  DocumentGuid,
  FamilySymbolId,
  PartType,
  ConnectorCount,
  SortedDiametersFingerprint,    // ex: "50,50,25"
  AngleMatrixRoundedFingerprint, // ex: "180:1,90:2"
  NormalizedTextsFingerprint,    // hash de NormalizeForSearch(family+type+desc)
  RelevantTypeParamsFingerprint  // se tiver params custom relevantes
)
```

**Estratégia de invalidação:**
- Nova classification per-run sempre, cache descartado entre runs
- Dentro de uma run, mesma cache key retorna identity cacheada
- Family paramétrica (mesmo Symbol, instance.params variam) → chave inclui instance.params, então cada instance entra na cache com chave própria

**Bypass cache quando:**
- `TopologyReadResult.Diagnostics` tem severity ≥ Warning
- `PartType=Undefined` (forçar fresh inference)
- Confidence anterior ficou Low

---

## 16. Confidence float + reason codes (Codex M#15)

### 16.1 Schema

```csharp
public sealed record ClassificationConfidence
{
    public required double Score { get; init; }                 // 0.0 .. 1.0
    public required ConfidenceBucket Bucket { get; init; }      // Low | Medium | High
    public required IReadOnlyList<string> Reasons { get; init; }
}

public enum ConfidenceBucket { Low, Medium, High }
```

### 16.2 Cálculo

```
score = startBase (0.5) +
        topologyMatchBonus (+0.3 se PartType nativo bate; +0.2 se inferido por geometria)
        - diagnosticsPenalty (sum of severity-weighted penalties)
        + lexicalScoreNormalized (0.0 .. 0.2)
        + lineDetectedBonus (+0.05 se Line ≠ Unknown)
        - disambiguatorUnvalidatedPenalty (-0.1 se houver)
        + partTypeNativeBonus (+0.05 se PartType ≠ Undefined/Other)

clamp(score, 0.0, 1.0)

bucket =
  Score ≥ 0.75  → High
  Score ≥ 0.45  → Medium
  Else          → Low
```

### 16.3 Reasons codes (exemplos)

```
TopologyMatched:Elbow90
PartTypeMatched:Elbow
PartTypeUndefined:InferredFromGeometry
PartTypeMismatchInferred:TeeButGeometrySaysWye
LexicalHint:joelho@familyName
LexicalHint:redux@familyName
LineDetected:redux
DisambiguatorPromoted:rosca→elbow-90-threaded
NegativeTokenSuppression:te[blockedBy:terminal]
DiagnosticPenalty:NonRoundConnectorIgnored
DiagnosticPenalty:MissingDiameter
LineUnknown:NoMatchInLexicalLines
```

### 16.4 UI behavior por bucket

| Bucket | Códigos Tigre | Tigre Quantifica | Perda de carga futura |
|---|---|---|---|
| **High** | aplica código auto | mostra normal | calcula auto |
| **Medium** | aplica se só 1 SKU candidato; senão prompt | yellow audit, mostra | calcula com warning |
| **Low** | não aplica auto, vai pra "Sem correspondência" | red audit | NÃO calcula |

---

## 17. `HydraulicLossKey` separado (Codex H#6) — futuro

Pra perda de carga (Crane TP-410 / Idelchik), `ConnectionIdentity` **não é suficiente**. Schema separado quando essa feature entrar:

```csharp
public sealed record HydraulicLossKey
{
    public required BaseKind BaseKind { get; init; }
    public required FlowPath FlowPath { get; init; }       // StraightThrough | BranchIn | BranchOut |
                                                            // Dividing | Combining | Reducing | Expanding
    public required int DnInletMm { get; init; }
    public required int DnOutletMm { get; init; }
    public double? AngleDeg { get; init; }                 // pra elbows/wyes
    public double? RadiusOverDiameter { get; init; }       // R/D pra curvas
    public ValveKind? ValveKind { get; init; }
    public double? ValveOpeningPercent { get; init; }      // pra Cv variável
    public ReducerKind? ReducerKind { get; init; }         // Concentric | Eccentric
    public MaterialClass MaterialClass { get; init; }      // PVC | CPVC | PEX | PPR | Steel | Copper
    public double? RoughnessMicrons { get; init; }
    public ReynoldsRange? Reynolds { get; init; }
}
```

**NÃO entra no MVP 1.** Anotado pra Slice futuro quando perda de carga for prioridade.

---

## 18. Subtipos cobertos no MVP 1

Lista canônica de subtypes do `pipe_connection_rules.json`:

| Subtype ID | BaseKind | GeometryKind | Angle | Notes |
|---|---|---|---|---|
| `cap` | Cap | Unspecified | null | 1 conector |
| `union-simple` | Union | Straight | 180 | 2 conectores Ø igual |
| `union-threaded` | Union | Straight | 180 | + Features.ThreadedEnd |
| `reducer-concentric` | Reducer | Straight | 180 | 2 conectores Ø diferente, axis aligned |
| `reducer-eccentric` | Reducer | Straight | 180 | 2 conectores Ø diferente, offset |
| `bushing` | Reducer | Straight | 180 | bucha de redução; pode ter Features |
| `adapter` | Reducer | Straight | 180 | adaptador caixa d'água/flange/rosca |
| `male-female-connector` | Reducer | Straight | 180 | Features.MaleEnd/FemaleEnd |
| `elbow-45` | Elbow | ShortRadius | 45 | 2 conectores Ø igual ângulo 45° |
| `elbow-90` | Elbow | ShortRadius | 90 | 2 conectores Ø igual ângulo 90° |
| `elbow-reducer` | Elbow | ShortRadius | 45 ou 90 | 2 conectores Ø diferente |
| `elbow-threaded` | Elbow | ShortRadius | 90 | + ThreadedEnd |
| `elbow-brass-bushing` | Elbow | ShortRadius | 90 | + BrassBushing |
| `elbow-visit` | Elbow | ShortRadius | 90 | + VisitCap |
| `long-radius-bend-45` | Elbow | LongRadius | 45 | curva longa |
| `long-radius-bend-90` | Elbow | LongRadius | 90 | curva longa |
| `transposition-curve` | Elbow | SShape | null | S-shape, 2 conectores colineares |
| `tee` | Tee | Branch | 90 | 3 conectores, lateral 90° |
| `tee-reducer` | Tee | Branch | 90 | + Reduced (DnBranch ≠ DnRun) |
| `tee-inspection` | Tee | Branch | 90 | + Inspection (4° conector tampa) |
| `tee-misturador` | Tee | Branch | 90 | PPR/Aquatherm |
| `tee-threaded` | Tee | Branch | 90 | + ThreadedEnd |
| `wye-simple` | Wye | Branch | 45 | junção simples, lateral 45° |
| `wye-reducer` | Wye | Branch | 45 | + Reduced |
| `wye-inverted` | Wye | Branch | 45 | + Inverted |
| `wye-double` | Cross | Multi | 45 | junção dupla, 2 laterais 45° |
| `cross` | Cross | Multi | 90 | cruzeta, 2 laterais 90° |
| `valve-shutoff` | Valve | Straight | 180 | ValveKind.Shutoff (esfera/gaveta/globo) |
| `valve-check` | Valve | Straight | 180 | ValveKind.Check (retenção) |
| `valve-prv` | Valve | Straight | 180 | ValveKind.PressureReducing |
| `valve-flush` | Valve | Straight | 180 | ValveKind.Flush (descarga) |
| `meter` | Valve | Straight | 180 | Category=Instrument, InstrumentKind.FlowMeter |
| `instrument-pressure` | Valve | Straight | 180 | InstrumentKind.PressureMeter |
| `manifold` | MultiPort | Multi | null | 3+ saídas |

Total: **~32 subtypes mapeados** no rulebook MVP 1.

---

## 19. JSON exemplo realista do rulebook (extrato)

```json
{
  "version": "2.0",
  "discipline": "Plumbing",
  "description": "Pipe connection rulebook — MVP 1 (post-Codex review 2026-05-28)",

  "baseKindTokens": {
    "elbow":     ["joelho", "curva"],
    "tee":       ["te", "tê"],
    "wye":       ["juncao", "wye"],
    "union":     ["luva", "uniao"],
    "reducer":   ["reducao", "bucha", "redutor", "adaptador"],
    "cap":       ["cap", "tampao", "plug"],
    "valve":     ["registro", "valvula", "esfera", "gaveta", "retencao"],
    "cross":     ["cruzeta", "cross"],
    "multiport": ["manifold", "barrilete", "distribuidor"]
  },

  "tokenAliases": {
    "te":      ["tê", "tee"],
    "juncao":  ["junção", "wye", "lateral"],
    "reducao": ["redução", "redutor"],
    "curva":   ["bend", "curve"],
    "uniao":   ["união", "coupling"],
    "joelho":  ["elbow", "cotovelo"]
  },

  "negativeTokens": {
    "te": ["terminal", "tempo", "teflon"],
    "sn": ["snake"]
  },

  "tolerances": {
    "angleDeg": 5,
    "diameterMm": 2
  },

  "rules": [
    {
      "id": "elbow-90",
      "baseKind": "Elbow",
      "geometryKind": "ShortRadius",
      "nominalAngleDeg": 90,
      "topology": {
        "partTypeAccepts": ["Elbow", "Other", "Undefined"],
        "connectorCount": 2,
        "diameterRule": {
          "mode": "roles",
          "constraints": [
            { "ports": ["RunA", "RunB"], "relation": "equal" }
          ]
        },
        "primaryAngleRule": { "minDeg": 85, "maxDeg": 95 }
      },
      "lexicalDisambiguators": [
        {
          "trigger": "curva",
          "promoteTo": "long-radius-bend-90",
          "mandatoryLexical": ["curva"]
        },
        {
          "trigger": "rosca",
          "promoteTo": "elbow-threaded",
          "mandatoryLexical": ["rosca"]
        },
        {
          "trigger": "bucha",
          "promoteTo": "elbow-brass-bushing",
          "mandatoryLexical": ["bucha", "latao"]
        },
        {
          "trigger": "transposicao",
          "promoteTo": "transposition-curve",
          "mandatoryLexical": ["transposicao"]
        },
        {
          "trigger": "visita",
          "promoteTo": "elbow-visit",
          "mandatoryLexical": ["visita"]
        }
      ]
    },

    {
      "id": "long-radius-bend-90",
      "baseKind": "Elbow",
      "geometryKind": "LongRadius",
      "nominalAngleDeg": 90,
      "topology": { "inherits": "elbow-90" },
      "requiresLexicalConfirmation": true
    },

    {
      "id": "elbow-reducer",
      "baseKind": "Elbow",
      "geometryKind": "ShortRadius",
      "topology": {
        "inherits": "elbow-90",
        "overrides": {
          "diameterRule": {
            "mode": "roles",
            "constraints": [
              { "ports": ["RunLarge", "RunSmall"], "relation": "different" }
            ]
          },
          "primaryAngleRule": { "minDeg": 40, "maxDeg": 95 }
        }
      },
      "lexicalHints": ["reducao"]
    },

    {
      "id": "tee",
      "baseKind": "Tee",
      "geometryKind": "Branch",
      "nominalAngleDeg": 90,
      "topology": {
        "partTypeAccepts": ["Tee", "LateralTee", "TapPerpendicular", "Other", "Undefined"],
        "connectorCount": 3,
        "diameterRule": {
          "mode": "roles",
          "constraints": [
            { "ports": ["RunA", "RunB"], "relation": "equal" }
          ]
        },
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 },
        "lateralAngleRule": { "minDeg": 85, "maxDeg": 95 }
      },
      "lexicalDisambiguators": [
        { "trigger": "inspecao", "promoteTo": "tee-inspection", "mandatoryLexical": ["inspecao"] },
        { "trigger": "misturador", "promoteTo": "tee-misturador", "mandatoryLexical": ["misturador"] },
        { "trigger": "reducao", "promoteTo": "tee-reducer", "mandatoryLexical": ["reducao"] }
      ]
    },

    {
      "id": "wye-simple",
      "baseKind": "Wye",
      "geometryKind": "Branch",
      "nominalAngleDeg": 45,
      "topology": {
        "partTypeAccepts": ["Wye", "LateralTee", "Tee", "Other", "Undefined"],
        "connectorCount": 3,
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 },
        "lateralAngleRule": { "minDeg": 40, "maxDeg": 50 }
      },
      "lexicalDisambiguators": [
        { "trigger": "dupla", "promoteTo": "wye-double" },
        { "trigger": "invertida", "promoteTo": "wye-inverted" },
        { "trigger": "reducao", "promoteTo": "wye-reducer" }
      ]
    }

    // ...mais ~25 rules
  ]
}
```

---

## 20. Migração `TigreCatalog` v1 → v2 (Codex M#14)

### 20.1 Pipeline híbrido (script + revisão manual)

```
INPUT: src/Core/DarivaBIM.Domain/Tigre/tigre_codes.json (v1, 872 SKUs)
       src/Core/DarivaBIM.Domain/Mep/Classification/Resources/pipe_connection_rules.json

SCRIPT: tools/migrate_tigre_catalog_v2.py

PIPELINE:
  pra cada entry no v1:
    1. Tokenize description
    2. Match contra rulebook (rodar classifier offline em "mode catalog")
    3. Suggest:
       baseKind (Elbow, Tee, Wye, ...)
       geometryKind
       nominalAngleDeg
       ports: [{role, dnMm}, ...]
       features
       line (do field productLine + lexicalLines lookup)
       confidenceScore (0..1)
    4. Generate row em CSV review

OUTPUT: docs/tigre-catalog-migration-review.csv

  sku,description,productLine,baseKind,geometryKind,angle,ports,features,line,confidence,needsReview,reason
  22150251,"Joelho 90 Soldável 25mm",Soldavel,Elbow,ShortRadius,90,"[RunA:25,RunB:25]",None,Soldavel,0.93,false,
  100002822,"Joelho 45 REDUX DN50",REDUX,Elbow,ShortRadius,45,"[RunA:50,RunB:50]",None,Redux,0.95,false,
  35217835,"Joelho 90 Soldável c/ Bucha de Latão 25x3/4""",Soldavel,Elbow,ShortRadius,90,"[RunA:25,RunB:DN20]",BrassBushing|ThreadedEnd,Soldavel,0.78,true,"Ambiguous Dn2 - 3/4 inch interpreted as 20mm"
  ...

MANUAL REVIEW STEP:
  Matheus revisa só rows com needsReview=true (esperado ~5-10%)
  Edita CSV manualmente quando necessário

REIMPORT STEP:
  Script lê CSV revisado + gera tigre_codes_v2.json
  Cada entry tem campos novos: baseKind, geometryKind, angle, ports[], features, line, manualReview:bool

VALIDATION STEP:
  Tests unitários: 100% das entries têm baseKind ≠ Unknown
  Smoke ratio: > 90% das entries com confidence ≥ 0.7
  Diff vs v1: 100% dos códigos preservados, descrição idêntica
```

### 20.2 Coexistência v1/v2

`TigreCatalog v1` mantido como fallback até v2 ser smoke-validated:

```csharp
public sealed class TigreCatalogResolver
{
    private readonly TigreCatalogV2 _v2;
    private readonly TigreCatalog _v1Fallback;

    public TigreCode? Resolve(ConnectionIdentity identity, Element element)
    {
        // v2 primeiro (estruturado)
        var match = _v2.FindMatch(identity, element);
        if (match != null) return match;

        // Fallback v1 (texto-puro) — pra peças que v2 não consegue classificar ainda
        // (geometria estranha, family custom muito ruim, etc)
        return _v1Fallback.FindMatch(...);
    }
}
```

Flag de feature toggle:
```csharp
public bool UseV2ClassifierAsPrimary { get; set; } = true;  // default ON após smoke
```

---

## 21. Algoritmo de classificação completo

```
INPUT:
  element: Revit.Element (fitting/accessory)

PIPELINE:
  // CAMADA 0 — ADAPTER
  topologyResult: TopologyReadResult = ConnectionTopologyReader.Read(element)
  texts: ElementTexts = ElementTextsReader.Read(element)
    // {familyName, typeName, description}

  // CAMADA 1 — VALIDAÇÃO DE TOPOLOGIA
  Se !topologyResult.Success:
    return ConnectionIdentity { BaseKind=Unknown, Confidence.Score=0, ... }

  // CAMADA 2 — FILTRO POR REGRAS COMPATÍVEIS
  candidates = rulebook.Rules.Where(r =>
      r.topology.Compatible(topologyResult.Topology)
  )
  Se candidates.Count == 0:
    return ConnectionIdentity { BaseKind=topologyResult.Topology.InferredBaseKind,
                                Confidence.Score=0.3, Bucket=Low,
                                Reasons=["NoMatchingRule"] }

  // CAMADA 3 — SCORE LEXICAL (hints híbridos com tokenizer rigoroso)
  scoredText = {
    familyName:  LexicalNormalizer.Tokenize(texts.familyName,  tokenizerOpts),
    typeName:    LexicalNormalizer.Tokenize(texts.typeName,    tokenizerOpts),
    description: LexicalNormalizer.Tokenize(texts.description, tokenizerOpts)
  }

  pra cada candidate em candidates:
    effectiveHints =
        rulebook.baseKindTokens[candidate.baseKind]
        UNION candidate.manualExtraHints
        UNION candidate.autoDerivedHints  // se classifier roda em modo "catalog"
    effectiveHints = ExpandAliases(effectiveHints, rulebook.tokenAliases)
    effectiveHints = StripNegatives(effectiveHints, scoredText.allConcatenated,
                                    rulebook.negativeTokens)

    candidate.lexicalScore = 0
    pra cada hint em effectiveHints:
      se hint in scoredText.familyName:   candidate.lexicalScore += 3
      se hint in scoredText.typeName:     candidate.lexicalScore += 2
      se hint in scoredText.description:  candidate.lexicalScore += 1

  winner = argmax(candidates, c => c.lexicalScore)
  Se empate: prioriza candidate sem requiresLexicalConfirmation
  Se ainda empate: primeiro da ordem do JSON

  // CAMADA 4 — DISAMBIGUATORS COM VALIDAÇÃO
  pra cada disambiguator em winner.lexicalDisambiguators:
    se disambiguator.trigger ∈ scoredText.allConcatenated:
      child = rules[disambiguator.promoteTo]
      se child.topology.Compatible(topologyResult.Topology):
        se all tokens disambiguator.mandatoryLexical ∈ scoredText.allConcatenated:
          winner = child
          Reasons.add("DisambiguatorPromoted:{trigger}→{promoteTo}")
          break

  // CAMADA 5 — DETECTAR LINHA
  Line line = ProductLine.Unknown
  pra cada (lineId, lineTokens) em rulebook.lexicalLines:
    se any token in lineTokens ∈ scoredText.allConcatenated:
      line = lineId
      Reasons.add("LineDetected:{lineId}")
      break

  // CAMADA 6 — DETECTAR FEATURES (flagged enum)
  features = Feature.None
  Se "rosca" ∈ scoredText: features |= Feature.ThreadedEnd
  Se "bucha latao" ∈ scoredText: features |= Feature.BrassBushing
  Se "visita" ∈ scoredText: features |= Feature.VisitCap
  Se "invertida" ∈ scoredText: features |= Feature.Inverted
  Se topologyResult.Topology.HasReduction: features |= Feature.Reduced
  // ... etc

  // CAMADA 7 — COMPUTE CONFIDENCE
  score = ComputeConfidenceScore(topologyResult, winner, lexicalScoreNormalized,
                                  features, line, diagnostics)

  // CAMADA 8 — BUILD IDENTITY
  return ConnectionIdentity {
    Discipline = topologyResult.Topology.InferredDiscipline,
    Category = topologyResult.Topology.InferredCategory,
    BaseKind = winner.baseKind,
    GeometryKind = winner.geometryKind,
    NominalAngleDeg = winner.nominalAngleDeg,
    Ports = topologyResult.Topology.Ports,  // já com Role assigned
    Features = features,
    Line = line,
    Confidence = {
      Score = score,
      Bucket = ToBucket(score),
      Reasons = accumulatedReasons
    }
  }
```

---

## 22. Integração com Códigos Tigre (consumer)

```csharp
public sealed class TigreCatalogV2
{
    public TigreCatalogEntry? FindMatch(ConnectionIdentity identity, Element element)
    {
        // 1. PRÉ-FILTRO ESTRUTURADO (rápido)
        var candidates = _entriesIndex.Where(e =>
            e.BaseKind == identity.BaseKind &&
            (e.GeometryKind == GeometryKind.Unspecified ||
             e.GeometryKind == identity.GeometryKind) &&
            (e.NominalAngleDeg == null ||
             Math.Abs(e.NominalAngleDeg.Value - identity.NominalAngleDeg.GetValueOrDefault()) <= 5) &&
            (identity.Line == ProductLine.Unknown ||
             e.ProductLine == identity.Line) &&
            DnsCompatible(e, identity.Ports) &&
            FeaturesCompatible(e.Features, identity.Features)
        ).ToList();

        if (candidates.Count == 0) return null;     // sem SKU; audit issue
        if (candidates.Count == 1) return candidates[0];

        // 2. DESAMPATE: bonus Tigre: Descrição preenchido (peso 5)
        var tigreDesc = element.LookupParameter("Tigre: Descrição")?.AsString();
        if (!string.IsNullOrWhiteSpace(tigreDesc))
        {
            var descTokens = LexicalNormalizer.Tokenize(tigreDesc, _opts);
            foreach (var cand in candidates)
            {
                var entryTokens = LexicalNormalizer.Tokenize(cand.description, _opts);
                cand.bonus = TokensIntersection(entryTokens, descTokens) * 5;
            }
        }

        // 3. FALLBACK: score por descrição completa do catálogo vs família+tipo
        var familyText = element.LookupParameter("Família")?.AsString() ?? "";
        var typeText = element.LookupParameter("Tipo")?.AsString() ?? "";

        foreach (var cand in candidates)
        {
            var entryTokens = LexicalNormalizer.Tokenize(cand.description, _opts);
            cand.fallbackScore =
                TokensIntersection(entryTokens, LexicalNormalizer.Tokenize(familyText, _opts)) * 3 +
                TokensIntersection(entryTokens, LexicalNormalizer.Tokenize(typeText, _opts)) * 2;
        }

        return candidates.OrderByDescending(c => c.bonus + c.fallbackScore).First();
    }
}
```

**Vantagens vs v1:**
- Pré-filtro estruturado é O(log N) (índice por baseKind) em vez de O(N) tokenizando tudo
- AmbiguityGuard substituído por desempate explícito (bonus + fallback score)
- Tigre: Descrição é bonus opcional, não pré-requisito

---

## 23. Plano de implementação MVP 1–4

| Fase | Escopo | Tempo | Dependências | Codex review |
|---|---|---|---|---|
| **MVP 1.A** | `Mep.Classification.Ports` (MepPort, PortRole) + `Mep.Classification.Connections` (BaseKind, GeometryKind, Feature, ConnectionIdentity, ConnectionTopology, TopologyReadResult). Domain pure. Tests headless. | ~4-5h | — | ao fim |
| **MVP 1.B** | `Adapter.Mep.ConnectionTopologyReader` + `ConnectorPhysicalFilter` + `RevitPartTypeMapper`. Tests com fixtures sintéticos (XYZ + BasisZ manipulados). | ~4-5h | 1.A | ao fim |
| **MVP 2.A** | `LexicalNormalizer` (boundary + accent + alias + negative) + tests cobrindo edge cases (te/terminal, sn/snake, acentos). | ~2h | — | combinado com 2.B |
| **MVP 2.B** | `pipe_connection_rules.json` v2 com schema formal (inherits objeto, diameterRule port-based, disambiguators validados, lexical hints híbrida) + `ConnectionRulebook.Classify` + tests integrando 1.A + 1.B + 2.A. | ~5-6h | 1.A, 1.B, 2.A | ao fim |
| **MVP 3.A** | Script `tools/derive_lexical_hints.py` + `tools/migrate_tigre_catalog_v2.py`. Gera `tigre_codes_v2.json` + CSV de revisão. | ~3h | 2.B | review do CSV manual |
| **MVP 3.B** | `TigreCatalogV2` consumer (filtra por ConnectionIdentity + Tigre: Descrição bonus) + `TigreCatalogResolver` coexistência v1/v2 com feature toggle. | ~3-4h | 3.A | ao fim |
| **MVP 4** | Re-smoke em modelo Tigre real (3 cenários: family paramétrica 45/90, tê vs junção, fittings custom). Codex review final do MVP completo. Ajustes finais. Push pra PR pra main. | ~3-4h | 1-3 completos | smoke gate humano |
| **Total MVP 1** | | **~24-29h** | | 4 codex reviews |

### Off-MVP (futuros)

- `Mep.HydraulicLoss` com `HydraulicLossKey` (perda de carga Crane TP-410/Idelchik)
- `Mep.Classification.Fixture` (caixas sifonadas, ralos, corpo caixa) — rulebook separado
- `Mep.Classification.Consumable` (anel, abraçadeira, suportes)
- `DuctFittingRulebook` (HVAC)
- `ConduitFittingRulebook` (elétrica)
- `GasFittingRulebook` (gás)
- UI: confidence bucket exposto em audit (Yellow/Red)
- Telemetria: anonymized classification confidence histograms

---

## 24. Off-limits / NÃO fazer agora

❌ **NÃO** implementar perda de carga junto com classifier. `HydraulicLossKey` separado, feature futura.

❌ **NÃO** mexer em `TigreCatalog v1` (matcher atual). Coexistência via `TigreCatalogResolver` com feature toggle.

❌ **NÃO** tentar cobrir HVAC/elétrica/gás no MVP. Rulebooks separados, fora do escopo.

❌ **NÃO** persistir cache em disco. Per-run only.

❌ **NÃO** criar GUID novo de shared param. Tigre: Descrição já existe via `LookupParameter`.

❌ **NÃO** mexer em `TigreDetectionRules` (Domain pure detector) — está validado, é decisão cristalizada.

❌ **NÃO** mexer em `RibbonCommandId` / `CommandRegistry` (wiring preservado).

❌ **NÃO** quebrar `LayerIsolationTests` — Domain `Mep.Classification` permanece sem RevitAPI.

❌ **NÃO** force-push em `claude/quantifica-followup-2026-05-27`. Push limpo, sem `--no-verify`.

❌ **NÃO** mergear pra `main` sem aprovação humana (Matheus) + Codex review por fase.

❌ **NÃO** implementar todas as features de UI (confidence buckets em audit) no MVP 1.B. Foco em backend.

---

## 25. Prompt template pra próximo agent

Quando spawnar teammate pra começar implementação (próxima sessão), use o prompt abaixo. Substitui `<FASE>` pelo nome da fase (`MVP 1.A`, `MVP 1.B`, etc) que será trabalhada.

```
Você é teammate único do Agent Team EVT-BIM. Lead é Claude Opus 4.7. Esta sessão NÃO tem
SendMessage/TaskGet/TaskUpdate/TaskList disponíveis (deferred tools). Workaround: prompt
autocontido + push pré-autorizado por Codex 0 BLOCKER.

═══════════════════════════════════════════════════════════════════
CONTEXTO
═══════════════════════════════════════════════════════════════════

- Repo: C:\Dariva-Codes\EVT-BIM
- Branch: claude/quantifica-followup-2026-05-27 (NÃO criar branch nova)
- HEAD esperado: <SHA atual após `git pull --ff-only`>
- Stack: .NET 8, Revit 2025+2026 via Shared Project .SharedSource, WPF, Clean Architecture
- Tigre é cliente pagante de contrato fechado — LicenseRequirement.Free permanente

ANTES DE QUALQUER COISA:
1. `git pull --ff-only` confirma branch atualizada
2. Ler `CLAUDE.md` raiz — 6 princípios não-negociáveis
3. Ler `docs/handoff-evtbim-agent-team.md` seções 2 e 9 (decisões cristalizadas + R4 workflow)
4. **LER `docs/mep-connection-rulebook.md` INTEIRO** — design canônico do que vamos implementar
5. Ler memória `C:\Users\mathe\.claude\projects\C--Dariva-Codes-EVT-BIM\memory\project_evtbim_tigre.md`
   pra estado pós-Slice 4.x

═══════════════════════════════════════════════════════════════════
ESCOPO DESTA FASE: <FASE>
═══════════════════════════════════════════════════════════════════

Implementar conforme seção 23 do `docs/mep-connection-rulebook.md`, fase <FASE>.

Resumo:
- <FASE description detalhada — copiar da seção 23 do doc>
- Arquivos a criar/modificar: <lista>
- Tests: <lista>

═══════════════════════════════════════════════════════════════════
PRINCÍPIOS NÃO-NEGOCIÁVEIS
═══════════════════════════════════════════════════════════════════

1. **Clean Architecture estrita:** Domain pure → Application → Adapters Revit → Plugin/Wpf.
   `Mep.Classification` (Domain) NÃO PODE referenciar Autodesk.Revit.* nem System.Windows.*.
   `LayerIsolationTests` + `ForbiddenUsingsScanner` quebram build se vazar.

2. **`record` / POCO immutability:** `ConnectionIdentity`, `MepPort`, etc são records (init-only).

3. **`record struct` shim:** se usar `record struct`, lembrar shim de IsExternalInit em netstandard2.0.

4. **`ElementId.Value` (long)**, não `IntegerValue`.

5. **R4 obrigatório antes de cada commit:**
   ```
   dotnet build DarivaBIM.V2025.slnf -p:SkipRevitDeploy=true   → 0 erros, 0 avisos
   dotnet build DarivaBIM.V2026.slnf -p:SkipRevitDeploy=true   → 0 erros, 0 avisos
   dotnet test src\tests\DarivaBIM.Core.Tests\DarivaBIM.Core.Tests.csproj  → 100% pass
   dotnet test src\tests\DarivaBIM.Architecture.Tests\DarivaBIM.Architecture.Tests.csproj → 100% pass
   ```

6. **Conventional commits + co-author** "Claude Opus 4.7 (1M context)" via HEREDOC.

7. **`git add` por path explícito** (NÃO `-A`, NÃO `.`).

8. **Codex review high-effort PRÉ-PUSH:** roda code-review skill no diff (ou solicita lead).
   - 0 BLOCKER → push autorizado por antecipação
   - NITS aceitáveis → anota no commit
   - BLOCKER persistente → para, NÃO push, reporta no output final

9. **Push limpo:** sem `--force`, sem `--no-verify`. Branch `claude/quantifica-followup-2026-05-27`.

10. **Off-limits** (NÃO TOCAR):
    - TigreCatalog v1 (matcher atual)
    - TigreDetectionRules (Domain detector, validado)
    - RibbonCommandId / CommandRegistry
    - Stashes
    - `tigre_codes.json` v1 (criar v2 separado em migration)
    - Outras features (TigreQuantifica, etc) exceto se explicitamente no escopo

═══════════════════════════════════════════════════════════════════
RELATÓRIO FINAL (no `result` do task-notification)
═══════════════════════════════════════════════════════════════════

- SHA HEAD em origin após push + sync 0/0
- Lista SHAs dos commits granulares
- Resumo Codex review (BLOCKERs, NITS aceitos)
- Decisões arquiteturais relevantes que tomou
- R4 final (V2025+V2026, Core, Architecture)
- Próxima fase pendente (referência seção 23 do doc)
- Se travou: diagnóstico literal + estado working tree

GO.
```

---

## Fim do documento — referência canônica

Quando implementação começar, consulte este doc seção por seção. Atualizações pontuais (campos novos no schema, regras adicionais no JSON, etc) podem ser commitadas conforme a fase avança — sempre mantendo a numeração das seções consistente.
