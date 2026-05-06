# Migração Dynamo → Plugin: FloorDrainExtension (Prolongador em caixas)

## 1. Objetivo da ferramenta

Criar prolongadores (tubos verticais) acima de caixas sifonadas/secas
selecionadas pelo usuário, herdando o diâmetro do conector vertical da
caixa e escolhendo um `PipeType` compatível com o material detectado
(Redux / Reforçada / Série Normal).

## 2. Script original

- **Dynamo (.dyn)**: `Prolongador em caixas.dyn`.
- **Python (.py)**: lógica embutida — escolhe sistema, detecta material,
  encontra conector vertical e cria o tubo.
- **Data**: anterior à versão 0.1 do plugin.

## 3. Entradas

| Entrada no Dynamo                | Tipo                | Vai para                                                                  |
|----------------------------------|---------------------|---------------------------------------------------------------------------|
| Comprimento (m)                  | double              | `FloorDrainExtensionWindow` (TextBox) → `FloorDrainExtensionCreator.Run`  |
| Caixas selecionadas              | List<FamilyInstance> | `PickObjects` em `FloorDrainExtensionExternalEvent` (Plugin)              |

## 4. Saídas

| Saída                            | Tipo                | Vai para                                                                  |
|----------------------------------|---------------------|---------------------------------------------------------------------------|
| Quantos prolongadores foram criados | int              | `FloorDrainExtensionResult.Created`                                       |
| Falhas categorizadas             | int                 | `FloorDrainExtensionResult.FailedNoVerticalConnector` / `FailedNoPipeType` / `FailedOther` |
| Logs por caixa                   | List<string>        | `FloorDrainExtensionResult.Logs`                                          |
| Mensagem na janela e TaskDialog  | string              | `FloorDrainExtensionWindow.SetStatus` + `TaskDialog` quando 0 created     |

## 5. Etapas do script original

| Ordem | Etapa                                                       | Etiqueta            | Destino no plugin                                                                            |
|-----:|-------------------------------------------------------------|---------------------|----------------------------------------------------------------------------------------------|
| 1     | Pedir comprimento (mm/m)                                    | `[INPUT]` `[UI]`    | `FloorDrainExtensionWindow.LengthTextBox` (Plugin Ui)                                        |
| 2     | Pedir seleção de caixas (`PickObjects`)                     | `[SELECTION]`       | `FloorDrainExtensionExternalEvent` + `PlumbingFixtureSelectionFilter` (Plugin)               |
| 3     | Resolver `PipingSystemType` (preferência: esgoto/sanitário) | `[FILTER]`          | `Adapter/Features/FloorDrainExtension/FloorDrainExtensionSystemTypeResolver`                 |
| 4     | Para cada caixa, encontrar o conector vertical              | —                   | `Adapter/Features/FloorDrainExtension/VerticalConnectorFinder`                               |
| 5     | Detectar material (redux/reforçada/série normal)            | `[CALCULATE]`       | `Adapter/Features/FloorDrainExtension/FloorDrainExtensionPipeTypeResolver.DetermineMaterialKind` |
| 6     | Escolher `PipeType` compatível                              | `[FILTER]`          | `Adapter/Features/FloorDrainExtension/FloorDrainExtensionPipeTypeResolver.FindPipeType`      |
| 7     | Resolver `LevelId` (elem.LevelId → ViewPlan → primeiro)     | —                   | `Adapter/Features/FloorDrainExtension/FloorDrainExtensionLevelResolver`                      |
| 8     | Calcular ponto inicial/final do tubo                        | `[CALCULATE]`       | `FloorDrainExtensionCreator.Run` (`p0`, `p1`)                                                |
| 9     | Abrir transação                                             | `[TRANSACTION]`     | `FloorDrainExtensionCreator.Run`                                                             |
| 10    | Criar `Pipe.Create`                                         | `[CREATE_ELEMENT]`  | `FloorDrainExtensionCreator.Run`                                                             |
| 11    | Ajustar diâmetro (`RBS_PIPE_DIAMETER_PARAM`)                | `[WRITE_PARAM]`     | `FloorDrainExtensionCreator.Run`                                                             |
| 12    | Conectar tubo no conector da caixa                          | —                   | `Adapter/Features/FloorDrainExtension/FloorDrainExtensionPipeConnector.ConnectPipeToFixture` |
| 13    | Acumular logs por caixa                                     | `[RESULT]`          | `FloorDrainExtensionResult.Logs`                                                             |
| 14    | Mostrar status + TaskDialog se 0 created                    | `[UI]`              | `FloorDrainExtensionExternalEvent.Execute` (Plugin)                                          |

## 6. Arquivos criados

### Plugin

- `Features/FloorDrainExtension/FloorDrainExtensionButton.cs`
- `Features/FloorDrainExtension/FloorDrainExtensionFeature.cs`
- `Features/FloorDrainExtension/ShowFloorDrainExtensionCommand.cs`
- `Features/FloorDrainExtension/FloorDrainExtensionExternalEvent.cs`
- `Ui/FloorDrainExtensionWindow.xaml(.cs)`

### Application

- (sem UseCase próprio hoje — fluxo é todo Adapter, mediado pela Plugin
  Feature; criar um se a regra precisar viver fora do Revit).

### Domain

- `Core/DarivaBIM.Domain/Tigre/TigreTextUtils.cs` (Normalize — usado para
  detectar materiais sem se preocupar com acentos/case).

### Revit Adapter

- `Features/FloorDrainExtension/FloorDrainExtensionCreator.cs` (orquestrador)
- `Features/FloorDrainExtension/FloorDrainExtensionResult.cs`
- `Features/FloorDrainExtension/VerticalConnectorFinder.cs`
- `Features/FloorDrainExtension/FloorDrainExtensionPipeTypeResolver.cs`
- `Features/FloorDrainExtension/FloorDrainExtensionSystemTypeResolver.cs`
- `Features/FloorDrainExtension/FloorDrainExtensionLevelResolver.cs`
- `Features/FloorDrainExtension/FloorDrainExtensionPipeConnector.cs`

### Infrastructure

- (sem persistência específica).

### Presentation

- (a janela ainda mora em `Plugin/Ui/`. Mover ViewModel para
  `Presentation.Wpf` é uma pendência reconhecida em ADR-0010.)

## 7. Fluxo final

```
RibbonButton "Adicionar Prolongadores"
   → ShowFloorDrainExtensionCommand (Plugin)
   → FloorDrainExtensionWindow (Plugin Ui)
       → user types comprimento + FloorDrainExtensionExternalEvent (Plugin Feature)
           → PickObjects(FamilyInstance) com PlumbingFixtureSelectionFilter
           → FloorDrainExtensionCreator (Adapter Feature)
                → FloorDrainExtensionSystemTypeResolver (Adapter Feature)
                → para cada caixa:
                    → VerticalConnectorFinder
                    → FloorDrainExtensionPipeTypeResolver
                    → FloorDrainExtensionLevelResolver
                    → Pipe.Create + set diameter
                    → FloorDrainExtensionPipeConnector
           ← FloorDrainExtensionResult
       → status na janela + TaskDialog se 0 created
```

## 8. Observações

- O `PlumbingFixtureSelectionFilter` aceita também `OST_PipeAccessory` /
  `OST_GenericModel` / `OST_MechanicalEquipment` para tolerar caixas
  cadastradas em categorias "soltas" — replica o que o script Dynamo
  encontrava.
- Quando nenhuma caixa for selecionada ou todas falharem, a UI ainda
  mostra um TaskDialog com as primeiras 40 linhas do log para o usuário
  diagnosticar.

## 9. Pendências

- Mover `FloorDrainExtensionWindow`/`ViewModel` para `Presentation.Wpf` (depende
  da extração do enum `Discipline` e de outras dependências — ADR-0010).
- Avaliar se faz sentido um UseCase de Application (`CreateFloorDrainExtensionsUseCase`)
  para concentrar a lógica de "como o sistema decide tipo/sistema" longe
  do Revit, com testes em fakes de adapter.
