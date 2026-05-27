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

## 4. O que são `lexicalHints` e `lexicalDisambiguators`

Esses dois blocos no JSON **não são** sobre fontes do elemento — são sobre **quais tokens caracterizam cada subtipo**. Ficam no JSON por subtype, não por elemento.

### `lexicalHints` — tokens **típicos** que descrevem o subtype

Lista de palavras que normalmente aparecem em FamilyName/TypeName/Description quando o elemento é desse subtipo. Usados pra:

- **Validar / aumentar confiança** do match topológico (topologia é Elbow+90°, e "joelho" aparece no nome → confiança alta).
- **Tiebreak** entre múltiplos candidatos topologicamente compatíveis (`joelho-90` e `curva-90` têm mesma topologia; quem tem mais hints no texto vence).

Exemplo:
```json
"joelho-90": { "lexicalHints": ["joelho", "curva", "90"] }
"te":        { "lexicalHints": ["te"] }
"juncao-simples": { "lexicalHints": ["juncao"] }
```

### `lexicalDisambiguators` — palavra-chave promove pra subtipo-filho

Mapa `palavra → subtype-id` que **reclassifica** o match pra uma variante específica quando o token aparece no texto.

Exemplo:
```json
"joelho-90": {
  "lexicalHints": ["joelho", "curva", "90"],
  "lexicalDisambiguators": {
    "curva":        "curva-90",
    "transposicao": "curva-transposicao",
    "rosca":        "joelho-90-rosca",
    "bucha":        "joelho-90-bucha-latao"
  }
}
```

Lógica: o classifier acha `joelho-90` topologicamente. Procura disambiguators no texto. Se nenhum bate, mantém `joelho-90`. Se "rosca" aparece, promove pra `joelho-90-rosca` (subtype-filho).

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
    candidate.score = 0
    pra cada hint em candidate.lexicalHints:
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

## 6. Subtipos que faltam mapear (pra você checar se considera importantes)

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

## 7. Perguntas pra você antes da implementação

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

## 8. Estimativa de implementação após sua aprovação do schema

| Fase | Escopo | Tempo |
|---|---|---|
| 1 | `ConnectionTopology` + `ConnectionTopologyReader` (Adapter Revit) + tests | ~3h |
| 2 | `connection_rules.json` finalizado + `ConnectionRulebook.Classify` + tests | ~4h |
| 3 | Refator `TigreCatalogV2` consumindo `ConnectionIdentity` (deprecando matcher antigo via flag) | ~4h |
| 4 | Re-smoke + Codex review final + ajustes | ~2-3h |

**Total: ~13-15h.** Codex review por fase.
