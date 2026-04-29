# ADR-0013 — Arquitetura genérica para Shared Parameters

- Status: Aceito
- Data: 2026-04-29

## Contexto

A primeira ferramenta a precisar de um shared parameter no plugin foi a
`TigreCodes`. A implementação inicial (em
`Adapter/Parameters/TigreSharedParameter.cs`) misturava:

- **Dados específicos do parâmetro** — nome, grupo, GUID, categoria-alvo.
- **Lógica genérica de criação/binding** — abrir/criar arquivo de Shared
  Parameters, garantir grupo + ExternalDefinition, montar `CategorySet`
  preservando categorias existentes, criar `InstanceBinding`/`TypeBinding`,
  Insert/ReInsert com fallbacks, lidar com parâmetros pré-existentes que
  têm o mesmo nome mas GUID diferente, migrar de type para instância
  quando o usuário pede etc.

À medida que o plugin ganhar mais shared parameters (`Tigre: Pressão`,
`Tigre: Vazão`, parâmetros institucionais), copiar essa lógica em cada
ferramenta criaria duplicação massiva e abriria espaço para divergências
sutis (uma ferramenta restaura o `SharedParametersFilename`, outra esquece;
uma preserva categorias existentes, outra atropela; uma migra type→instance
e outra falha).

## Decisão

Adotar um modelo **declarativo + serviço** para shared parameters:

1. **`SharedParameterDefinition`** (em
   `Adapter/Common/SharedParameters/`) descreve um shared parameter como
   *dado*: nome, grupo, GUID, `SpecTypeId`, `ParameterGroupTypeId`,
   categorias, `BindingKind` (instance/type), `Visible`, `UserModifiable`.

2. **`SharedParameterService`** (em
   `Adapter/Common/SharedParameters/`) concentra a lógica genérica:
   - `Ensure(Document doc, SharedParameterDefinition def)` →
     `SharedParameterEnsureResult`. Cria/atualiza o binding e devolve a
     ação executada + lista de avisos.
   - `GetParameter(Element element, SharedParameterDefinition def)` →
     `Parameter?` (busca por nome → GUID).

3. **Componentes auxiliares dedicados a uma única responsabilidade**:
   - `SharedParameterFileService` — abre/cria o arquivo de Shared
     Parameters; restaura o caminho anterior.
   - `ProjectParameterBindingService` — `BindingMap`, `CategorySet`,
     Insert/ReInsert com fallback.
   - `SharedParameterAccessor` — busca de parâmetro em um elemento.
   - `ExistingSharedParameterBindingInfo` — snapshot interno do binding
     existente.
   - `SharedParameterEnsureResult` — saída agregada (`Action`, `Warnings`).
   - `SharedParameterBindingKind` — enum `Instance`/`Type`.

4. **Cada feature só declara seus parâmetros**, em
   `Adapter/Features/<Nome>/SharedParameters/<Nome>SharedParameters.cs`.

## Consequências

- **Adicionar novo shared parameter vira operação declarativa**:
  declarar a definição + chamar `SharedParameterService.Ensure(...)`.
- **Reaproveitamos lógica de criação e binding** entre todas as
  ferramentas; bugs no fluxo (ex.: não restaurar `SharedParametersFilename`)
  são corrigidos uma vez para todas.
- **Identidade do parâmetro fica clara**: a constante de identidade
  (`Name`/`Guid`) pode viver no Domain como `*SharedParameterDefinition`
  pura (já existe `Domain.Tigre.TigreSharedParameterDefinition`), e o
  Adapter só monta a definição completa com tipos do RevitAPI.
- **Comportamento preservado**: o serviço replica o que o script Dynamo
  fazia — incluindo o aproveitamento de parâmetros pré-existentes pelo
  nome (com `Warning`) e a tentativa de migração type→instance.
- **`Adapter/Parameters/TigreSharedParameter.cs` foi removido**. Usuários
  internos foram migrados nesta entrega: `TigreCodeApplier` agora chama
  `SharedParameterService.Ensure` e `.GetParameter` direto, sem a classe
  intermediária.
- **Custo**: cada feature passa a depender da abstração
  `SharedParameterDefinition` em vez de uma classe estática própria. Em
  troca, a quantidade de código duplicado a manter por ferramenta cai
  para zero.

## Pendências reconhecidas

- `Common/SharedParameters/Definitions/` (definições institucionais
  reutilizáveis) será criado quando a primeira definição
  multi-ferramenta surgir. Hoje só existe a declaração específica de
  `TigreCodes`.
- O `SpecTypeId` exigido pelo construtor é não-anulável; como o tipo
  `ForgeTypeId` em Revit 2024+ funciona como handle, isso é aceitável,
  mas se outra plataforma (Revit 2023) for adicionada e quebrar a
  assinatura, o construtor pode receber `string` + factory por versão.

## Referências

- ADR-0001 — Clean architecture (Domain agnóstico)
- ADR-0003 — Plugin vs Adapter
- ADR-0012 — Organização por Feature no Revit Adapter
- `src/docs/guides/shared-parameters-architecture.md`
- `src/Revit/DarivaBIM.Revit.Adapters.V2026/Common/SharedParameters/`
- `src/Revit/DarivaBIM.Revit.Adapters.V2026/Features/TigreCodes/SharedParameters/TigreCodesSharedParameters.cs`
- `src/Core/DarivaBIM.Domain/Tigre/TigreSharedParameterDefinition.cs`
