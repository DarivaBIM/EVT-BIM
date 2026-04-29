# Arquitetura genérica de Shared Parameters

> Decisão registrada em
> `src/docs/adr/ADR-0013-shared-parameters-architecture.md`.

A criação e o binding de shared parameters no Revit envolvem várias etapas
quase mecânicas — abrir o arquivo de Shared Parameters, garantir grupo,
criar a `ExternalDefinition`, montar o `CategorySet`, escolher
`InstanceBinding`/`TypeBinding`, lidar com fallbacks de Insert/ReInsert,
considerar parâmetros pré-existentes com mesmo nome mas GUID diferente, e
assim por diante. Em vez de cada feature carregar essa lógica, o plugin
adota um modelo **declarativo + serviço**.

## Princípios

1. **Shared parameters são dados.** Cada parâmetro é descrito por uma
   instância de
   `DarivaBIM.Revit.Adapters.V2026.Common.SharedParameters.SharedParameterDefinition`:
   nome, grupo, GUID, `SpecTypeId`, `ParameterGroupTypeId`, categorias,
   binding kind (instance/type), `Visible`, `UserModifiable`.
2. **Toda a lógica de criação/binding mora em um único serviço.**
   `SharedParameterService.Ensure(doc, definition)` faz o trabalho de
   inspecionar o binding existente, criar o que falta, migrar de type↔instance
   quando possível, e devolver um `SharedParameterEnsureResult` com `Action`
   e `Warnings` para o usuário.
3. **Acesso é uniforme.** `SharedParameterService.GetParameter(element, definition)`
   busca primeiro pelo nome (para reaproveitar parâmetros pré-existentes),
   depois pelo GUID — espelhando o comportamento dos scripts Dynamo.
4. **Cada feature só declara seu(s) parâmetro(s).** Sem reimplementar
   criação, binding ou file management.

## Como adicionar um novo shared parameter

1. Em `Adapter/Features/<Nome>/SharedParameters/<Nome>SharedParameters.cs`,
   declare a definição:

   ```csharp
   public static readonly SharedParameterDefinition Code = new SharedParameterDefinition(
       name: "Tigre: Código",
       groupName: "Tigre",
       guid: new Guid("71ba9de5-ea50-4906-bbf6-4e86df006f48"),
       specTypeId: SpecTypeId.Int.Integer,
       parameterGroupTypeId: GroupTypeId.Data,
       categories: new[] { BuiltInCategory.OST_PipeCurves },
       bindingKind: SharedParameterBindingKind.Instance,
       visible: true,
       userModifiable: true);
   ```

2. Garanta o parâmetro dentro da transação da sua feature:

   ```csharp
   SharedParameterEnsureResult ensure =
       SharedParameterService.Ensure(doc, MyFeatureSharedParameters.Code);
   report.ParameterAction = ensure.Action;
   report.Warnings.AddRange(ensure.Warnings);
   ```

3. Acesse o parâmetro em cada elemento:

   ```csharp
   Parameter? target = SharedParameterService.GetParameter(elem, MyFeatureSharedParameters.Code);
   if (target == null || target.IsReadOnly) return;
   target.Set(value);
   ```

## Onde colocar a definição

| Cenário                                                       | Local recomendado                                         |
|---------------------------------------------------------------|------------------------------------------------------------|
| Parâmetro usado por uma feature específica                    | `Adapter/Features/<Nome>/SharedParameters/<Nome>SharedParameters.cs` |
| Parâmetro institucional usado por várias features             | `Adapter/Common/SharedParameters/Definitions/<Nome>.cs` (criar a pasta quando surgir o primeiro caso) |
| Constantes "puras" (nome, grupo, GUID) para reuso em Domain   | `Domain/<Categoria>/<Nome>SharedParameterDefinition.cs` (sem RevitAPI) |

> Hoje o exemplo é `TigreSharedParameterDefinition` em
> `Domain.Tigre` (puro) + `TigreCodesSharedParameters.Code` em
> `Adapter.Features.TigreCodes.SharedParameters` (com RevitAPI).
> Consumir as constantes do Domain mantém o GUID do parâmetro como
> identidade compartilhada entre `Domain`, `Application` e Adapter.

## Comportamentos importantes

- **Parâmetro pré-existente com nome igual mas GUID diferente.** Em vez de
  falhar, o serviço aproveita o parâmetro pelo nome e adiciona um aviso ao
  `Warnings` (replicando o comportamento do script Dynamo). O usuário
  decide se ajusta manualmente.
- **Parâmetro existente como `Type`, mas a definição pede `Instance`.** O
  serviço tenta migrar via `ReInsert`/`Insert` e adiciona um aviso. Se o
  Revit recusar (alguns templates antigos não permitem), o serviço lança
  com instrução de ajuste manual.
- **Arquivo de Shared Parameters ausente.** O serviço cria um arquivo
  temporário em `%TEMP%/DarivaBIM_SharedParameters.txt`, garante o
  parâmetro, e restaura o caminho original na saída
  (`SharedParameterFileService.RestorePreviousPath`).
- **Categorias preservadas.** Quando o parâmetro já tinha um binding com
  outras categorias, o serviço preserva todas e acrescenta as faltantes —
  não derruba bindings de outros add-ins.

## Componentes internos

`Common/SharedParameters/` é dividido para deixar cada responsabilidade em
um arquivo só:

| Arquivo                                  | Responsabilidade                                              |
|------------------------------------------|----------------------------------------------------------------|
| `SharedParameterDefinition.cs`           | Dado declarativo (nome, GUID, tipo, categorias, kind, flags).  |
| `SharedParameterBindingKind.cs`          | Enum `Instance`/`Type`.                                        |
| `SharedParameterEnsureResult.cs`         | Saída de `Ensure` (`Action`, `Warnings`).                      |
| `ExistingSharedParameterBindingInfo.cs`  | Snapshot interno do binding existente.                         |
| `SharedParameterFileService.cs`          | Abre/cria o arquivo de Shared Parameters; restaura caminho.    |
| `ProjectParameterBindingService.cs`      | `BindingMap`: inspeção, `CategorySet`, Insert/ReInsert.        |
| `SharedParameterService.cs`              | Fluxo principal `Ensure` + `GetParameter`.                     |
| `SharedParameterAccessor.cs`             | Busca por nome → GUID em um elemento.                          |

Nada disso precisa ser tocado quando uma feature nova é adicionada — só a
declaração `SharedParameterDefinition` muda.

## Anti-padrões

- ❌ Reimplementar `BindingMap.ForwardIterator()` em cada ferramenta.
- ❌ Manter constantes `ParamName`/`ParamGuid` em uma classe dedicada com
  lógica embutida.
- ❌ Criar `app.SharedParametersFilename` e esquecer de restaurar.
- ❌ Mexer em binding sem preservar categorias já vinculadas (atropela
  outros add-ins do projeto).

## Anti-padrões corrigidos pelo refator

A feature `TigreCodes` antes vivia em
`Adapter/Parameters/TigreSharedParameter.cs` com toda a lógica embutida.
Após esta refatoração, ela virou:

- `Adapter/Features/TigreCodes/SharedParameters/TigreCodesSharedParameters.cs`:
  declaração de dados.
- Lógica reutilizada de `Adapter/Common/SharedParameters/`.

A próxima ferramenta que precisar de um shared parameter (ex.:
`Tigre: Pressão`, `Tigre: Vazão`) não duplica nada — só declara um novo
`SharedParameterDefinition`.
