# Migração Dynamo → Plugin: Prolongador

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

| Entrada no Dynamo                | Tipo                | Vai para                                                  |
|----------------------------------|---------------------|-----------------------------------------------------------|
| Comprimento (m)                  | double              | `ProlongadorWindow` (TextBox) → `ProlongadorCreator.Run`  |
| Caixas selecionadas              | List<FamilyInstance> | `PickObjects` em `ProlongadorExternalEvent` (Plugin)      |

## 4. Saídas

| Saída                            | Tipo                | Vai para                                                  |
|----------------------------------|---------------------|-----------------------------------------------------------|
| Quantos prolongadores foram criados | int              | `ProlongadorResult.Created`                               |
| Falhas categorizadas             | int                 | `ProlongadorResult.FailedNoVerticalConnector` / `FailedNoPipeType` / `FailedOther` |
| Logs por caixa                   | List<string>        | `ProlongadorResult.Logs`                                  |
| Mensagem na janela e TaskDialog  | string              | `ProlongadorWindow.SetStatus` + `TaskDialog` quando 0 created |

## 5. Etapas do script original

| Ordem | Etapa                                                       | Etiqueta            | Destino no plugin                                                              |
|-----:|-------------------------------------------------------------|---------------------|--------------------------------------------------------------------------------|
| 1     | Pedir comprimento (mm/m)                                    | `[INPUT]` `[UI]`    | `ProlongadorWindow.LengthTextBox` (Plugin Ui)                                  |
| 2     | Pedir seleção de caixas (`PickObjects`)                     | `[SELECTION]`       | `ProlongadorExternalEvent` + `PlumbingFixtureSelectionFilter` (Plugin)         |
| 3     | Resolver `PipingSystemType` (preferência: esgoto/sanitário) | `[FILTER]`          | `Adapter/Features/Prolongador/ProlongadorSystemTypeResolver`                   |
| 4     | Para cada caixa, encontrar o conector vertical              | —                   | `Adapter/Features/Prolongador/VerticalConnectorFinder`                         |
| 5     | Detectar material (redux/reforçada/série normal)            | `[CALCULATE]`       | `Adapter/Features/Prolongador/ProlongadorPipeTypeResolver.DetermineMaterialKind` |
| 6     | Escolher `PipeType` compatível                              | `[FILTER]`          | `Adapter/Features/Prolongador/ProlongadorPipeTypeResolver.FindPipeType`        |
| 7     | Resolver `LevelId` (elem.LevelId → ViewPlan → primeiro)     | —                   | `Adapter/Features/Prolongador/ProlongadorLevelResolver`                        |
| 8     | Calcular ponto inicial/final do tubo                        | `[CALCULATE]`       | `ProlongadorCreator.Run` (`p0`, `p1`)                                          |
| 9     | Abrir transação                                             | `[TRANSACTION]`     | `ProlongadorCreator.Run`                                                       |
| 10    | Criar `Pipe.Create`                                         | `[CREATE_ELEMENT]`  | `ProlongadorCreator.Run`                                                       |
| 11    | Ajustar diâmetro (`RBS_PIPE_DIAMETER_PARAM`)                | `[WRITE_PARAM]`     | `ProlongadorCreator.Run`                                                       |
| 12    | Conectar tubo no conector da caixa                          | —                   | `Adapter/Features/Prolongador/ProlongadorPipeConnector.ConnectPipeToFixture`   |
| 13    | Acumular logs por caixa                                     | `[RESULT]`          | `ProlongadorResult.Logs`                                                       |
| 14    | Mostrar status + TaskDialog se 0 created                    | `[UI]`              | `ProlongadorExternalEvent.Execute` (Plugin)                                    |

## 6. Arquivos criados

### Plugin

- `Features/Prolongador/ProlongadorButton.cs`
- `Features/Prolongador/ProlongadorFeature.cs`
- `Features/Prolongador/ShowProlongadorCommand.cs`
- `Features/Prolongador/ProlongadorExternalEvent.cs`
- `Ui/ProlongadorWindow.xaml(.cs)`

### Application

- (sem UseCase próprio hoje — fluxo é todo Adapter, mediado pela Plugin
  Feature; criar um se a regra precisar viver fora do Revit).

### Domain

- `Core/DarivaBIM.Domain/Tigre/TigreTextUtils.cs` (Normalize — usado para
  detectar materiais sem se preocupar com acentos/case).

### Revit Adapter

- `Features/Prolongador/ProlongadorCreator.cs` (orquestrador)
- `Features/Prolongador/ProlongadorResult.cs`
- `Features/Prolongador/VerticalConnectorFinder.cs`
- `Features/Prolongador/ProlongadorPipeTypeResolver.cs`
- `Features/Prolongador/ProlongadorSystemTypeResolver.cs`
- `Features/Prolongador/ProlongadorLevelResolver.cs`
- `Features/Prolongador/ProlongadorPipeConnector.cs`

### Infrastructure

- (sem persistência específica).

### Presentation

- (a janela ainda mora em `Plugin/Ui/`. Mover ViewModel para
  `Presentation.Wpf` é uma pendência reconhecida em ADR-0010.)

## 7. Fluxo final

```
RibbonButton "Prolongador em caixas"
   → ShowProlongadorCommand (Plugin)
   → ProlongadorWindow (Plugin Ui)
       → user types comprimento + ProlongadorExternalEvent (Plugin Feature)
           → PickObjects(FamilyInstance) com PlumbingFixtureSelectionFilter
           → ProlongadorCreator (Adapter Feature)
                → ProlongadorSystemTypeResolver (Adapter Feature)
                → para cada caixa:
                    → VerticalConnectorFinder
                    → ProlongadorPipeTypeResolver
                    → ProlongadorLevelResolver
                    → Pipe.Create + set diameter
                    → ProlongadorPipeConnector
           ← ProlongadorResult
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

- Mover `ProlongadorWindow`/`ViewModel` para `Presentation.Wpf` (depende
  da extração do enum `Discipline` e de outras dependências — ADR-0010).
- Avaliar se faz sentido um UseCase de Application (`CreateProlongadoresUseCase`)
  para concentrar a lógica de "como o sistema decide tipo/sistema" longe
  do Revit, com testes em fakes de adapter.
