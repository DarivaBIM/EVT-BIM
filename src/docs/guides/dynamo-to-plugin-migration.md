# Migrando um script Python/Dynamo para o plugin DarivaBIM

Este guia descreve o caminho recomendado quando uma ferramenta nasce como um
script Dynamo/Python e precisa virar uma feature do plugin DarivaBIM em C#.
A ideia é reaproveitar o que já existe (Plugin Features, Adapter Common,
SharedParameters etc.) e não duplicar lógica.

## 1. Mapa de destinos

A primeira tarefa, ao receber um script, é decidir para qual camada cada
trecho vai. A tabela abaixo é o resumo prático:

| Trecho do script Python/Dynamo                              | Onde colocar no plugin                                      |
|-------------------------------------------------------------|-------------------------------------------------------------|
| Inputs do Dynamo (sliders, dropdowns, text inputs, toggles) | Plugin Feature, ViewModel ou Request DTO                    |
| Seleção de elementos (`PickObject`, `PickObjects`)          | Plugin Feature / `ExternalEvent` / `Handler`                |
| `FilteredElementCollector`                                  | Adapter Feature / `Collector`                               |
| Filtros de categoria/disciplina                             | Adapter `Common/Filters` ou Adapter Feature                 |
| `LookupParameter` / `get_Parameter`                         | Adapter `Common/Parameters` (`RevitParameterReader`)         |
| Conversão de unidades (mm/m/°)                              | Adapter `Common/Units` (`RevitUnitConverter`)                |
| Cálculo puro (regra de negócio, sem RevitAPI)               | `Domain` (entidades, value objects, regras)                  |
| Regra de aplicação (orquestração entre adapters)            | `Application/UseCases`                                       |
| Escrita de parâmetros                                       | Adapter Feature (chamando `RevitParameterWriter`)            |
| Criação de elementos (Pipe, Wall, FamilyInstance...)        | Adapter Feature / `Creator`                                  |
| Transação                                                   | Adapter `Common/Transactions` (`RevitTransactionRunner`)     |
| Preprocessor de falhas                                      | Adapter `Common/Transactions/FailurePreprocessors`           |
| Resultado/logs (DTO de saída)                               | `Application` DTOs ou `Adapter Feature` `Result`             |
| Mensagem para o usuário (`TaskDialog`, status na ribbon)    | Plugin Tool ou Presenter                                     |
| Janela WPF / ViewModel                                      | `Presentation.Wpf`                                           |
| Shared parameters (criação, binding)                        | Adapter `Common/SharedParameters` (`SharedParameterService`) |
| Definição de shared parameter (nome, GUID, categorias)      | Adapter Feature / `SharedParameters/<Nome>SharedParameters` |

## 2. Etiquetas do roteiro

Antes de criar arquivos, marque cada trecho do script com uma das etiquetas
abaixo. Elas correspondem a destinos do mapa acima e ajudam a "atomizar" o
script em pedaços que cabem nas camadas certas.

| Etiqueta           | O que representa                                          |
|--------------------|------------------------------------------------------------|
| `[INPUT]`          | Entrada vinda do usuário (slider, textbox, dropdown).      |
| `[SELECTION]`      | Seleção de elementos no Revit (PickObject(s)).             |
| `[COLLECT]`        | Busca de elementos no documento (FilteredElementCollector).|
| `[READ_PARAM]`     | Leitura de parâmetro de um elemento.                       |
| `[FILTER]`         | Filtro/teste sobre um elemento ou conjunto.                |
| `[CALCULATE]`      | Cálculo puro (matemático, regra) sem RevitAPI.             |
| `[WRITE_PARAM]`    | Escrita de parâmetro em um elemento.                       |
| `[CREATE_ELEMENT]` | Criação de novo elemento (Pipe, FamilyInstance, ...).      |
| `[TRANSACTION]`    | Abertura/commit/rollback de Transaction.                   |
| `[RESULT]`         | Composição da saída/log da ferramenta.                     |
| `[UI]`             | Janela, mensagem, TaskDialog ou status visual.             |

## 3. Roteiro de migração

1. Cole o script original em `src/docs/dynamo-migration/<nome-da-ferramenta>.md`,
   usando `_template.md` como base.
2. Anote cada bloco com a etiqueta apropriada.
3. Para cada etiqueta, liste o destino-alvo (coluna "Vai para" no template).
4. Crie a feature no Plugin
   (`src/Plugins/DarivaBIM.Plugin.V2026/Features/<Nome>/`):
   `Button`, `Feature`, `Command`, `Tool` (se houver fluxo modeless: também
   `ExternalEvent` + `Handler`). Veja
   `src/docs/guides/how-to-create-a-tool.md`.
5. Crie a feature no Adapter
   (`src/Revit/DarivaBIM.Revit.Adapters.V2026/Features/<Nome>/`):
   `Collector`, `Reader`, `Writer`, `Creator`, `Resolver` etc., conforme
   as etiquetas que você marcou.
6. Se houver shared parameter novo, declare em
   `Features/<Nome>/SharedParameters/<Nome>SharedParameters.cs` e use
   `SharedParameterService`. Não copie a lógica de criação/binding — ela
   já vive em `Common/SharedParameters`.
7. Se houver cálculo puro (sem `Document`/`Element`/etc.), extraia para
   `Domain` e cubra com testes.
8. Se houver orquestração entre vários adapters, crie um UseCase em
   `Application/UseCases/<Nome>/` com interfaces no construtor.
9. Termine atualizando o documento de migração com os arquivos criados,
   o fluxo final (Button → Command → Tool → UseCase → Adapter → ...) e
   as pendências.

## 4. Erros comuns a evitar

- **Vazar RevitAPI no Domain/Application/Presentation.Wpf.** Se um cálculo
  precisa de `Element`/`Parameter`, ou ele lê valores na Adapter e
  passa DTOs para o Domain, ou ele continua na Adapter como helper interno.
- **Reimplementar criação de shared parameter.** Use
  `SharedParameterService.Ensure(...)`. Apenas declare a definição.
- **Copiar `ParamToText` em cada feature.** Use
  `ParameterTextReader.Read(doc, param)` em `Common/Parameters`.
- **Esquecer a transação.** Use `RevitTransactionRunner.Run(...)` quando o
  contexto não estiver dentro de outra transação aberta pelo caller.
- **Misturar UI com transação.** `TaskDialog` é OK no Plugin Tool; nunca em
  Adapter Feature. Adapter Feature retorna DTO, o Plugin formata.

## 5. Para ler em seguida

- `src/docs/guides/adapter-anatomy.md` — como o Adapter está organizado.
- `src/docs/guides/shared-parameters-architecture.md` — como adicionar shared
  parameter declarativamente.
- `src/docs/guides/how-to-create-a-tool.md` — passo-a-passo da feature do Plugin.
- `src/docs/dynamo-migration/_template.md` — template de documento de migração.
- `src/docs/adr/ADR-0012-revit-adapter-feature-organization.md` — decisão de
  organização do Adapter por feature.
- `src/docs/adr/ADR-0013-shared-parameters-architecture.md` — decisão da
  arquitetura genérica de shared parameters.
