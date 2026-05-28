# MEP Connection Rulebook — design proposto

> **Status:** rascunho pra revisão (2026-05-28)
> **Escopo:** módulo Domain reusable que classifica conexões MEP (joelho/curva/tê/junção/luva/redução/cap/etc) a partir de topologia geométrica + texto. Independente de Codificar Tigre — também alimenta perda de carga, gerenciador de famílias, validação NBR, etc.

---

## 1. Por que módulo Domain reusable

Hoje o matcher do Tigre Catalog é **texto-puro** dentro de `TigreCatalog.FindMatch`. Acoplado à feature Codificar Tigre. Problema:

- Não desambigua famílias paramétricas (Joelho 45_90)
- Confunde Tê vs Junção (mesma família, mesmo PartType, ângulo lateral diferente)
- Pra outras ferramentas futuras (perda de carga, gerenciador de famílias, geração de isométricos) precisaríamos duplicar a lógica

**Solução:** novo namespace `DarivaBIM.Domain.Mep.Connections` que classifica **qualquer** instância MEP num **subtipo canônico** (joelho-90, te-reducao, juncao-simples, etc), independente de marca/linha. Output do classifier: `ConnectionIdentity { Subtype: "joelho-90", PrimaryDn: 50, SecondaryDn: null, AngleDeg: 90, ConfidenceTopology, ConfidenceLexical }`.

Cada feature consome conforme precisa:
- **Códigos Tigre** filtra catálogo por `(subtype, primaryDn, secondaryDn)` + texto desambigua linha (Redux/SN/Soldável)
- **Perda de carga** consulta tabela `K-loss[subtype, dn, ...]`
- **Gerenciador de famílias** mostra famílias agrupadas por subtype
- **Validações NBR** verifica regras tipo "junção com curva precisa de Tê de inspeção a cada 10m"

---

## 2. Arquitetura proposta

```
src/Core/DarivaBIM.Domain/
  Mep/
    Connections/
      ConnectionTopology.cs           POCO: PartType + ConnectorCount +
                                      Diameters[] + Angles[] + HasReduction
      ConnectionSubtype.cs            enum/identifier (cap, joelho-45, juncao-simples, etc)
      ConnectionIdentity.cs           POCO: Subtype + PrimaryDn + SecondaryDn +
                                      AngleDeg + ConfidenceTopology + ConfidenceLexical
      ConnectionRule.cs               POCO de uma regra do JSON
      ConnectionRulebook.cs           Carrega rules + Classify(topology, texts)
      Resources/
        connection_rules.json         (embedded resource)

src/Revit/DarivaBIM.Revit.Adapters.SharedSource/
  Common/
    Mep/
      ConnectionTopologyReader.cs     Element → ConnectionTopology
                                      via FamilyInstance.MEPModel.ConnectorManager
```

**Layer isolation:** Domain `Mep.Connections` permanece **sem RevitAPI** (regras + classifier puro). Adapter Revit faz tradução de `Element` → `ConnectionTopology`. Reutilizável em qualquer feature.

---

## 3. JSON de regras — esboço pra você revisar

Arquivo proposto: `src/Core/DarivaBIM.Domain/Mep/Connections/Resources/connection_rules.json`

```json
{
  "version": "1.0",
  "description": "Regras de identificação de subtipos de conexões MEP hidráulicas. Cada subtipo tem assinatura topológica (PartType + conectores + ângulos + diâmetros) + dicas léxicas pra desambiguação textual.",

  "rules": [

    {
      "id": "cap",
      "displayName": "Cap / Tampão",
      "topology": {
        "partType": ["Cap", "Other", "Undefined"],
        "connectorCount": 1,
        "diameterRule": "single",
        "primaryAngleRule": null,
        "lateralAngleRule": null
      },
      "lexicalHints": ["cap", "tampao"],
      "lexicalDisambiguators": {
        "tampao": "cap"
      }
    },

    {
      "id": "luva-simples",
      "displayName": "Luva / União Simples (inline, mesma bitola)",
      "topology": {
        "partType": ["Union", "Other", "Undefined"],
        "connectorCount": 2,
        "diameterRule": "equal",
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 }
      },
      "lexicalHints": ["luva", "uniao"],
      "lexicalDisambiguators": {
        "uniao": "uniao-rosqueada",
        "porca": "uniao-rosqueada"
      }
    },

    {
      "id": "uniao-rosqueada",
      "displayName": "União com porca (desmontável)",
      "topology": "inherits luva-simples",
      "lexicalHints": ["uniao", "porca", "anel"],
      "requiresLexicalConfirmation": true
    },

    {
      "id": "bucha-reducao",
      "displayName": "Bucha de Redução / Luva de Redução / Adaptador",
      "topology": {
        "partType": ["Transition", "Other", "Undefined"],
        "connectorCount": 2,
        "diameterRule": "different",
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 }
      },
      "lexicalHints": ["bucha", "reducao", "adaptador", "conector"],
      "lexicalDisambiguators": {
        "bucha": "bucha-reducao",
        "adaptador": "adaptador",
        "conector": "conector"
      }
    },

    {
      "id": "adaptador",
      "displayName": "Adaptador soldável-rosca / Caixa d'água / Flange",
      "topology": "inherits bucha-reducao",
      "lexicalHints": ["adaptador", "flange", "caixa", "rosca"],
      "requiresLexicalConfirmation": true
    },

    {
      "id": "conector",
      "displayName": "Conector Macho/Fêmea (PEX, PPR, Aquatherm)",
      "topology": "inherits bucha-reducao",
      "lexicalHints": ["conector", "macho", "femea"],
      "requiresLexicalConfirmation": true
    },

    {
      "id": "joelho-45",
      "displayName": "Joelho 45° / Curva 45° (mesma bitola)",
      "topology": {
        "partType": ["Elbow", "Other", "Undefined"],
        "connectorCount": 2,
        "diameterRule": "equal",
        "primaryAngleRule": { "minDeg": 40, "maxDeg": 50 }
      },
      "lexicalHints": ["joelho", "curva", "45"],
      "lexicalDisambiguators": {
        "curva": "curva-45",
        "longa": "curva-45-longa"
      }
    },

    {
      "id": "curva-45",
      "displayName": "Curva 45° (raio longo)",
      "topology": "inherits joelho-45",
      "lexicalHints": ["curva", "45"],
      "requiresLexicalConfirmation": true
    },

    {
      "id": "joelho-90",
      "displayName": "Joelho 90° / Curva 90° (mesma bitola)",
      "topology": {
        "partType": ["Elbow", "Other", "Undefined"],
        "connectorCount": 2,
        "diameterRule": "equal",
        "primaryAngleRule": { "minDeg": 85, "maxDeg": 95 }
      },
      "lexicalHints": ["joelho", "curva", "90"],
      "lexicalDisambiguators": {
        "curva": "curva-90",
        "longa": "curva-90-longa",
        "curta": "curva-90-curta",
        "rosca": "joelho-90-rosca",
        "bucha": "joelho-90-bucha-latao",
        "transposicao": "curva-transposicao"
      }
    },

    {
      "id": "curva-90",
      "displayName": "Curva 90° (raio longo)",
      "topology": "inherits joelho-90",
      "lexicalHints": ["curva", "90"],
      "requiresLexicalConfirmation": true
    },

    {
      "id": "joelho-reducao",
      "displayName": "Joelho de Redução (45° ou 90°)",
      "topology": {
        "partType": ["Elbow", "Other", "Undefined"],
        "connectorCount": 2,
        "diameterRule": "different",
        "primaryAngleRule": { "minDeg": 40, "maxDeg": 95 }
      },
      "lexicalHints": ["joelho", "reducao"]
    },

    {
      "id": "curva-transposicao",
      "displayName": "Curva de Transposição (S-shape)",
      "topology": {
        "partType": ["Elbow", "Other", "Undefined"],
        "connectorCount": 2,
        "diameterRule": "equal",
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 },
        "geometryHint": "s-shape: 2 conectores colineares com offset radial"
      },
      "lexicalHints": ["transposicao", "transposição"],
      "requiresLexicalConfirmation": true
    },

    {
      "id": "te",
      "displayName": "Tê (3 conectores, lateral 90°)",
      "topology": {
        "partType": ["Tee", "Other", "Undefined"],
        "connectorCount": 3,
        "diameterRule": "anyMatchingTwoEqual",
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 },
        "lateralAngleRule": { "minDeg": 85, "maxDeg": 95 }
      },
      "lexicalHints": ["te"],
      "lexicalDisambiguators": {
        "inspecao": "te-inspecao",
        "misturador": "te-misturador",
        "macho": "te-rosca",
        "femea": "te-rosca",
        "bucha": "te-bucha-latao"
      }
    },

    {
      "id": "te-reducao",
      "displayName": "Tê de Redução (lateral com Ø diferente)",
      "topology": {
        "partType": ["Tee", "Other", "Undefined"],
        "connectorCount": 3,
        "diameterRule": "lateralDifferentFromInline",
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 },
        "lateralAngleRule": { "minDeg": 85, "maxDeg": 95 }
      },
      "lexicalHints": ["te", "reducao"]
    },

    {
      "id": "te-inspecao",
      "displayName": "Tê de Inspeção (com plug central)",
      "topology": "inherits te",
      "lexicalHints": ["inspecao"],
      "requiresLexicalConfirmation": true
    },

    {
      "id": "juncao-simples",
      "displayName": "Junção Simples (3 conectores, lateral 45°)",
      "topology": {
        "partType": ["Tee", "Other", "Undefined"],
        "connectorCount": 3,
        "diameterRule": "anyMatchingTwoEqual",
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 },
        "lateralAngleRule": { "minDeg": 40, "maxDeg": 50 }
      },
      "lexicalHints": ["juncao"],
      "lexicalDisambiguators": {
        "dupla": "juncao-dupla",
        "invertida": "juncao-invertida"
      }
    },

    {
      "id": "juncao-reducao",
      "displayName": "Junção Simples de Redução",
      "topology": "inherits juncao-simples, diameterRule=lateralDifferentFromInline",
      "lexicalHints": ["juncao", "reducao"]
    },

    {
      "id": "juncao-dupla",
      "displayName": "Junção Dupla (4 conectores, 2 laterais 45°)",
      "topology": {
        "partType": ["Tee", "Cross", "Other", "Undefined"],
        "connectorCount": 4,
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 },
        "lateralAngleRule": { "minDeg": 40, "maxDeg": 50, "count": 2 }
      },
      "lexicalHints": ["juncao", "dupla"]
    },

    {
      "id": "cruzeta",
      "displayName": "Cruzeta (4 conectores, 2 laterais 90°)",
      "topology": {
        "partType": ["Cross", "Other", "Undefined"],
        "connectorCount": 4,
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 },
        "lateralAngleRule": { "minDeg": 85, "maxDeg": 95, "count": 2 }
      },
      "lexicalHints": ["cruzeta", "cross"]
    },

    {
      "id": "registro",
      "displayName": "Registro / Válvula (PartType=Valve)",
      "topology": {
        "partType": ["Valve", "Other", "Undefined"],
        "connectorCount": 2,
        "diameterRule": "equal",
        "primaryAngleRule": { "minDeg": 175, "maxDeg": 185 }
      },
      "lexicalHints": ["registro", "valvula", "esfera", "gaveta", "retencao"]
    }

  ],

  "lexicalLines": {
    "soldavel": ["soldavel"],
    "roscavel": ["roscavel", "rosca"],
    "redux": ["redux"],
    "serie-normal": ["sn", "serie normal", "esg sn"],
    "serie-reforcada": ["sr", "serie reforcada"],
    "aquatherm": ["aquatherm"],
    "clicpex": ["clicpex", "pex"],
    "ppr": ["ppr"],
    "tigrefire": ["tigrefire", "cpvc"]
  },

  "tolerances": {
    "angleDeg": 5,
    "diameterMm": 2
  }
}
```

---

## 4. lexicalHints — estratégia híbrida (decidida 2026-05-28)

**Princípio:** preencher manualmente todos os hints possíveis é frágil (esquece aliases, manutenção alta, quebra com cada novo fabricante). Estratégia escolhida: **10% manual + 90% derivado automaticamente do catálogo.**

### 4.1 Manual (~55 entries fixas, raramente mudam)

#### `baseKindTokens` — universais do português hidráulico

Palavras-chave que mapeiam pra cada BaseKind. ~25 tokens totais. Definidos uma vez no JSON do rulebook.

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
}
```

#### `tokenAliases` — sinônimos / variações regionais

Dicionário declarativo (~20 entries) que normaliza sinônimos entre fabricantes/idiomas/grafias.

```json
"tokenAliases": {
  "te":       ["tê", "tee"],
  "juncao":   ["junção", "wye", "lateral"],
  "reducao":  ["redução", "redutor"],
  "curva":    ["bend", "curve"],
  "uniao":    ["união", "coupling"],
  "joelho":   ["elbow", "cotovelo"]
}
```

Crítico pra cobertura cross-fabricante (Tigre "Curva" = Krona "Joelho" = manufacturer gringo "Elbow").

#### `negativeTokens` — anti-falsos-positivos

Lista de tokens que **NÃO** devem ativar a regra, mesmo aparecendo no texto. Adicionar sob demanda quando smoke detectar falso positivo.

```json
"negativeTokens": {
  "te":   ["terminal", "tempo", "teflon"],
  "sn":   ["snake"],
  "sr":   ["sra", "sranho"]
}
```

~10 entries iniciais; cresce com smoke real.

### 4.2 Auto-derivado (centenas/milhares de hints, manutenção zero)

Script Python `tools/derive_lexical_hints.py` roda como parte da geração do `tigre_codes.json v2`. Para cada SKU:

```python
def derive_hints(entry: dict, base_kind_tokens: dict, aliases: dict) -> dict:
    """
    entry = {
      "description": "Joelho 45 REDUX DN50",
      "code": 100002822,
      "subtype": "elbow",          # já populado por sugestão automática
      "productLine": "REDUX",
      "dn1": 50, "dn2": None, ...
    }

    Pipeline:
    1. Tokenize description (boundary-aware, accent-stripped, lowercase)
       → tokens = ["joelho", "45", "redux", "dn50"]
    2. Remove números DN/dimensões via regex (\\d+|dn\\d+|pn\\d+)
       → tokens = ["joelho", "redux"]
    3. Remove tokens já cobertos pelo baseKindTokens[subtype]
       (esses são genéricos, não diferenciam SKU específica)
       → tokens = ["redux"]
    4. Expand aliases (cada token + seus sinônimos do dicionário)
       → tokens = ["redux"]
    5. Cross-reference productLine field do JSON catalog (linha autoritativa)
       → confirma "redux" via productLine="REDUX"
    6. Resultado: entry.autoDerivedHints = ["redux"]
    """
```

Cada entry do catálogo final tem `autoDerivedHints` populado **automaticamente**. Quando catálogo Amanco/Astra/Krona for adicionado, hints deles vêm grátis do mesmo script.

### 4.3 lexicalLines auto-derivada

Dicionário `lexicalLines` (`Redux`/`SerieNormal`/`Soldavel`/etc) também extraído automaticamente do field `productLine` do catálogo:

```python
def derive_lexical_lines(catalog: list[dict]) -> dict:
    """
    catalog tem entries com productLine field.

    Agrupa por productLine único, gera tokens canônicos:
      Tigre: ["REDUX", "SN", "SR", "Aquatherm", "ClicPEX", "PPR", "TigreFire"]
      → "Redux":       ["redux"]
        "SerieNormal": ["sn", "serie normal", "esg sn"]
        "Aquatherm":   ["aquatherm"]
        ...
    """
```

Novo fabricante adiciona seus `productLine` → dicionário cresce sem editar regras.

### 4.4 Como o classifier usa os 2 conjuntos (manual + derivado)

No score lexical (passo 3 do exemplo anterior):

```
candidate "elbow"  (BaseKind=Elbow)
  hints_efetivos =
    baseKindTokens["elbow"]            // ["joelho", "curva"] — universal
    UNION
    entry.autoDerivedHints              // ["redux"] — específica do SKU "Joelho 45 REDUX DN50"
  hints_efetivos = expand(aliases, hints_efetivos)   // ["joelho", "curva", "elbow", "cotovelo", "redux"]
  hints_efetivos = remove(negative_in_context, hints_efetivos)  // sem mudança nesse exemplo

  pra cada hint:
    if hint in FamilyName:   score += 3
    if hint in TypeName:     score += 2
    if hint in Description:  score += 1
```

A regra do JSON só precisa declarar **BaseKind** (`elbow`, `tee`, etc). Os hints específicos vêm de:
1. `baseKindTokens` (universais, manuais)
2. `autoDerivedHints` por entry do catálogo (automáticas)
3. `aliases` expansão (manuais, declarativos)

### 4.5 lexicalDisambiguators — palavra-chave promove pra subtipo-filho

Mapa `palavra → subtype-id` que **reclassifica** o match pra uma variante específica.

Exemplo:
```json
"elbow-90": {
  "lexicalDisambiguators": {
    "rosca":        "elbow-90-threaded",
    "bucha":        "elbow-90-brass-bushing",
    "transposicao": "transposition-curve"
  }
}
```

**Validação obrigatória (Codex HIGH#10):** disambiguator só promove se o subtipo-filho é **topologicamente compatível** com a topology atual + tem `mandatoryLexical` tokens presentes (se declarados). Evita promoção indevida (`"curva"` reclassificando joelho que tecnicamente não é curva).

Lógica:
```
para cada (palavra → filho) em disambiguators:
  se palavra ∈ texto:
    se filho.topology.matches(currentTopology):   // validação topológica
      se all mandatoryLexical do filho ∈ texto:    // validação léxica
        winner = filho
        break
```

### 4.6 Custo manutenção comparado

| Cenário | Manual original | Híbrido |
|---|---|---|
| Setup inicial | ~3h preenchendo JSON | ~1h (manual) + ~1h (script Python) |
| Adicionar Amanco (200 SKUs) | ~2h preenchendo hints novos | **zero** (catálogo carrega via script) |
| Adicionar Krona (150 SKUs) | ~2h | **zero** |
| Adicionar novo subtype | ~10min editando JSON | ~5min se baseKind novo, **zero** se reusa |
| Falso positivo em smoke | 5min adicionando exclusão | 5min adicionando `negativeTokens` |
| Aliases gringo→pt-BR | propenso a esquecer | dicionário declarativo, audit fácil |

---

## 5. Algoritmo de classificação (refinado — score universal)

Score usa **só fontes universais** (FamilyName/TypeName/Description). Esses params existem em **qualquer** família Revit, não dependem de shared params específicos Tigre. Mantém o módulo `Mep.Connections` reusável por outras ferramentas (perda de carga, gerenciador de famílias, etc).

```
ConnectionIdentity Classify(topology, familyName, typeName, description):

  // CAMADA 1 — TOPOLOGIA: filtra regras compatíveis
  candidates = rules.Where(r => r.topology.matches(topology))
  Se candidates.Count == 0:
    return ConnectionIdentity { Subtype: Unknown, Confidence: Low }

  // CAMADA 2 — SCORE LÉXICO: desempata entre candidates topologicamente compatíveis
  allText = NormalizeForSearch(familyName + " " + typeName + " " + description)
  // (3 fontes universais: peso por origem)
  scoredText = {
    "familyName":  NormalizeForSearch(familyName),   // peso 3
    "typeName":    NormalizeForSearch(typeName),     // peso 2
    "description": NormalizeForSearch(description)   // peso 1
  }

  pra cada candidate em candidates:
    // hints híbridos: BaseKind universal + auto-derivado do catálogo + aliases expand
    candidate.effectiveHints =
      rulebook.baseKindTokens[candidate.baseKind]    // ["joelho", "curva"] etc
      UNION
      candidate.autoDerivedHints                      // ["redux"] da entry do catálogo
    candidate.effectiveHints = expandAliases(candidate.effectiveHints)
    candidate.effectiveHints = stripNegatives(candidate.effectiveHints, allText)

    candidate.score = 0
    pra cada hint em candidate.effectiveHints:
      se hint in scoredText.familyName:   candidate.score += 3
      se hint in scoredText.typeName:     candidate.score += 2
      se hint in scoredText.description:  candidate.score += 1

  winner = argmax(candidates.score)
  Se empate persistente:
    1) prioriza candidate sem requiresLexicalConfirmation
    2) prioriza candidate que apareceu primeiro no JSON

  // CAMADA 3 — DISAMBIGUATORS: promove pra subtype-filho se palavra-chave bate
  pra cada (palavra → subtypeFilho) em winner.lexicalDisambiguators:
    se palavra ∈ allText:
      winner = rules[subtypeFilho]
      break

  // CAMADA 4 — LINHA: identifica material/linha (sempre opcional, não bloqueia)
  linha = matchInLexicalLines(allText)
    // ex: "ESG_Redux_Joelho 45_90" → linha = "redux"

  return ConnectionIdentity {
    Subtype = winner.id,
    PrimaryDn = topology.diameters[0],
    SecondaryDn = topology.diameters[1] se reduction,
    AngleDeg = topology.primaryAngleDeg,
    Line = linha,    // opcional — só preenchido se algum token de linha matchou
    ConfidenceTopology = "high"|"medium"|"low",
    ConfidenceLexical = "high"|"medium"|"low"
  }
```

**Nota arquitetural:** o classifier NÃO conhece `Tigre: Descrição` nem outros shared params específicos de fabricante. Esses só entram **a jusante**, no consumer (ex: `TigreCatalogV2.FindMatch` faz seu próprio bonus de match contra `Tigre: Descrição` depois do classifier produzir o `ConnectionIdentity`).

---

## 6. Integração com Códigos Tigre (consumer-side)

A camada "Tigre: Descrição" entra **só aqui**, sem poluir `Mep.Connections`:

```
TigreCatalogV2.FindMatch(connectionIdentity, element):

  // PRE-FILTER por estrutura (já temos via classifier)
  candidates = entries.Where(e =>
    e.subtype == connectionIdentity.Subtype
    AND e.dn1 ≈ connectionIdentity.PrimaryDn (±tolerância)
    AND (connectionIdentity.SecondaryDn == null OR e.dn2 ≈ connectionIdentity.SecondaryDn)
    AND (connectionIdentity.Line == null OR e.productLine == connectionIdentity.Line)
  )

  Se candidates.Count == 0: return null  // audit "Tigre não tem SKU"
  Se candidates.Count == 1: return candidates[0]

  // TIEBREAK específico Tigre: bonus se "Tigre: Descrição" preenchido bate
  tigreDescricao = LookupParameter(element, "Tigre: Descrição")
  Se tigreDescricao não-vazio:
    pra cada candidate em candidates:
      bonus = tokensIntersection(candidate.description, tigreDescricao) * 5  // peso alto

  // Fallback: score por descrição completa do catalog vs família+tipo
  // (mesma lógica universal do classifier mas comparando contra entry.description
  // em vez de subtype hints)
  return argmax(candidates.score + bonus)
```

Vantagem: `Mep.Connections` permanece agnóstico de fabricante; `TigreCatalogV2` usa Tigre: Descrição como bonus opcional sem amarrar o módulo Domain à Tigre.

---

## 7. Subtipos que faltam mapear (pra você checar se considera importantes)

- Adaptador para Bico (TIGREFire) — Transition + lexical "bico"
- Joelho com Bucha de Latão / com Rosca — Elbow + lexical
- Joelho 90 c/ visita (SN) — Elbow + lexical "visita"
- Curva de Transposição (S-shape) — exige geometria especial
- Cruzeta (4 conectores 90°) — incluída
- Terminal de Ventilação — single connector + lexical "ventilacao"
- Caixa Sifonada / Caixa Seca / Ralo / Corpo Caixa — geometria especial; categoria PlumbingFixtures, não PipeFitting. Talvez ficam fora desse rulebook (Plumbing Fixtures podem usar matcher simples-texto)
- Válvula de Retenção / Esfera / Gaveta — incluídas em "registro"
- Manifold ClicPEX (2-3 saídas) — variante de Cruzeta/Tê com mais conectores
- Tê Misturador (PPR/Aquatherm) — Tê + lexical "misturador" + rosca

---

## 8. Perguntas pra você antes da implementação

1. **Aceita o schema acima?** Quer adicionar/remover algum subtipo?
2. ~~`lexicalDisambiguators` simples ou score elaborado?~~ ✅ Decidido (2026-05-28):
   - Score universal `FamilyName×3 + TypeName×2 + Description×1` (sem Tigre: Descrição)
   - `lexicalHints` valida/desempata candidatos topológicos
   - `lexicalDisambiguators` promove pra subtype-filho via palavra-chave
   - Tigre: Descrição entra só no consumer-side (TigreCatalogV2), não no classifier genérico
3. **Caixas Sifonadas / Ralos / Corpo Caixa** ficam fora do rulebook (são PlumbingFixtures, geometria não-cilíndrica) ou criamos categoria separada?
4. **Tubos** ficam fora (só conexões aqui)? Tubo é Pipe, não FamilyInstance — não tem ConnectorManager da mesma forma. Já funciona razoável no matcher atual via texto. Manteria fora?
5. **PartType "Other"/"Undefined"** sempre permite match topológico (inferimos via topologia mesmo) ou só algumas regras aceitam?
6. **Confidence reportada como output:** vale expor pra UI? Audit Yellow quando confidence=Low?

---

## 9. Estimativa de implementação após sua aprovação do schema

| Fase | Escopo | Tempo |
|---|---|---|
| 1 | `ConnectionTopology` + `ConnectionTopologyReader` (Adapter Revit) + tests | ~3h |
| 2 | `connection_rules.json` finalizado + `ConnectionRulebook.Classify` + tests | ~4h |
| 3 | Refator `TigreCatalogV2` consumindo `ConnectionIdentity` (deprecando matcher antigo via flag) | ~4h |
| 4 | Re-smoke + Codex review final + ajustes | ~2-3h |

**Total: ~13-15h.** Codex review por fase.
