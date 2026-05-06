# ADR-0012 — Organização por Feature no Revit Adapter

- Status: Aceito
- Data: 2026-04-29

## Contexto

Após o ADR-0010 (Plugin fino) e o ADR-0011 (organização por Feature no
Plugin), o `Plugin.V2026` ficou cristalino: cada ferramenta tem uma pasta
central que agrupa botão, comando, tool, external events e manifesto.

A camada `Revit.Adapters.V2026`, porém, continuou estruturada por
"categoria de RevitAPI" (`Writers/`, `Mapping/`, `Cad/`, `Pipes/`,
`Filters/`, `Parameters/`, `Transactions/`). Isso gerava três problemas:

1. **Para entender uma ferramenta** o leitor precisava abrir várias
   pastas (`Writers/TigreCodeApplier.cs`, `Parameters/TigreSharedParameter.cs`,
   `Filters/...`, `Mapping/...`) e correlacionar arquivos pelo nome.
2. **Para migrar um script Dynamo/Python** não havia um destino óbvio: o
   desenvolvedor tinha que decidir, para cada bloco, em qual pasta
   "categoria" ele entrava.
3. **Reuso vs. especificidade** ficava confuso: `TigreSharedParameter`
   carregava lógica genérica de criação de shared parameter junto com os
   dados específicos do parâmetro Tigre — bloqueando o reuso para outras
   ferramentas.

## Decisão

Organizar `DarivaBIM.Revit.Adapters.V2026` em duas grandes pastas:

1. **`Common/`** — blocos reutilizáveis de RevitAPI:
   - `Cad/`, `Elements/`, `Filters/`, `Parameters/`, `Pipes/`, `Selection/`,
     `SharedParameters/`, `Transactions/` (com `FailurePreprocessors/`),
     `Units/`.
   - Exemplos: `SharedParameterService`, `RevitParameterReader`,
     `RevitParameterWriter`, `ParameterTextReader`, `RevitTransactionRunner`,
     `RevitUnitConverter`, `CadGeometryExtractor`, `CadSegmentExtractor`,
     `PipeConnectorService`, `PipeCreationFailurePreprocessor`,
     `CadCurveSelectionFilter`.

2. **`Features/<Nome>/`** — implementação Revit específica de cada
   ferramenta:
   - `TigreCodes/`, `PipeCadMapper/`, `FloorDrainExtension/`, `BatchParameterEditor/`,
     `FamiliesImporter/`.
   - Sufixos canônicos por papel: `Collector`, `Reader`, `Writer`,
     `Creator`, `Applier`, `Resolver`, `Finder`.
   - `SharedParameters/` aninhado quando a feature declara seus shared
     parameters (ex.: `Features/TigreCodes/SharedParameters/TigreCodesSharedParameters.cs`).

A regra prática para decidir: se o código responde *"como esta ferramenta
funciona?"*, vai em `Features/`. Se responde *"como faço algo genérico no
Revit?"*, vai em `Common/`.

## Consequências

- **Localizar o código real de uma ferramenta vira óbvio**: abrir
  `Features/<Nome>/`. Junto com a feature equivalente em `Plugin.V2026`,
  forma uma "história" linear da ferramenta.
- **Migrar scripts Python/Dynamo fica viável**: cada bloco do script tem
  um destino canônico (ver `src/docs/guides/dynamo-to-plugin-migration.md`
  e `src/docs/dynamo-migration/_template.md`).
- **Reuso explícito**: helpers como `SharedParameterService`,
  `RevitParameterReader`, `RevitTransactionRunner`, `RevitUnitConverter`
  não ficam mais escondidos dentro de uma feature.
- **Pastas legadas (`Writers/`, `Mapping/`, `Cad/`, `Pipes/`, `Filters/`,
  `Parameters/`, `Transactions/`) foram removidas**. Os arquivos mudaram
  de namespace (`Adapters.V2026.Writers` → `Adapters.V2026.Common.<X>` ou
  `Adapters.V2026.Features.<X>`). `git mv` preserva histórico.
- **Custo**: usuários da Adapter precisam atualizar `using` quando
  consomem tipos antes em `Adapters.V2026.Writers`/`.Mapping`/etc.
  Endereçado nesta entrega — busca por namespaces antigos no repositório
  retorna zero ocorrências.

## Pendências reconhecidas

- `Common/Selection/` e `Common/Elements/` foram criados com a estrutura,
  mas ainda sem helpers; serão preenchidos quando a próxima feature
  precisar.
- `Features/PipeCadMapper/PipeCreator.cs` ainda é uma fachada estática
  (orquestrador) — se outra ferramenta vier a precisar de criação de tubos
  com regras diferentes, faz sentido extrair `PipePlaceholderCreator` e
  `PipePlaceholderConverter` (já feito) para subir um nível em `Common/`.
- O `RevitElementIdConversions` permanece em
  `Plugin.V2026/Features/PipeCadMapper/Tools/` enquanto for usado só por
  uma feature; mover para `Adapter/Common/Elements/ElementIdConverter.cs`
  vira lógico assim que uma segunda feature precisar.

## Referências

- ADR-0001 — Clean architecture (Domain agnóstico)
- ADR-0003 — Plugin vs Adapter
- ADR-0010 — Plugin fino, Composition Root e Presentation.Wpf neutra
- ADR-0011 — Organização por Feature no Plugin
- ADR-0013 — Arquitetura genérica para Shared Parameters
- `src/docs/guides/adapter-anatomy.md`
- `src/docs/guides/dynamo-to-plugin-migration.md`
