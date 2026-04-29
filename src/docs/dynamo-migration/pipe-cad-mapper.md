# Migração Dynamo → Plugin: PipeCADMapper

## 1. Objetivo da ferramenta

Converter linhas de vínculos CAD (DWG/DXF) em tubos do Revit, conectando
automaticamente segmentos adjacentes e tubos pré-existentes encontrados no
modelo, com sistema, tipo, diâmetro e nível escolhidos pelo usuário em uma
janela WPF modeless.

## 2. Script original

- **Dynamo (.dyn)**: Conjunto Dynamo+Player original (`PipeCADMapper.dyn`).
- **Python (.py)**: Lógica embutida em `PipeFromCAD.py` (PickObject + criação
  de placeholders + ConvertPipePlaceholders + ligação por proximidade).
- **Data**: anterior à versão 0.1 do plugin.

## 3. Entradas

| Entrada no Dynamo                  | Tipo                | Vai para                                                 |
|------------------------------------|----------------------|----------------------------------------------------------|
| Sistema (PipingSystemType)         | Element via dropdown | `PipeConverterViewModel.SelectedSystem` (long id)        |
| Tipo de tubo                       | Element via dropdown | `PipeConverterViewModel.SelectedPipeType`                |
| Diâmetro nominal (mm)              | double               | `PipeConverterViewModel.SelectedDiameterMm`              |
| Nível de referência                | Level                | `PipeConverterViewModel.SelectedLevel`                   |
| Offset acima do nível (mm)         | double               | `PipeConverterViewModel.OffsetMm`                        |
| Pick: linha de vínculo CAD         | Reference            | `PipeInsertionHandler.Execute` → `Reference`             |

## 4. Saídas

| Saída                              | Tipo                 | Vai para                                                 |
|------------------------------------|----------------------|----------------------------------------------------------|
| Tubos criados                      | int                  | `PipeCreationResult.CreatedCount`                        |
| Segmentos pulados (curtos)         | int                  | `PipeCreationResult.SkippedCount`                        |
| Arcos convertidos como cordas      | int                  | `PipeCreationResult.ArcsAsChordCount`                    |
| Status na janela                   | string               | `PipeInsertionStatusFormatter.Format`                    |

## 5. Etapas do script original

| Ordem | Etapa                                                       | Etiqueta            | Destino no plugin                                                              |
|-----:|-------------------------------------------------------------|---------------------|--------------------------------------------------------------------------------|
| 1     | Abrir janela com sistema/tipo/diâmetro/nível/offset         | `[INPUT]` `[UI]`    | `PipeConverterWindow` (Plugin Ui) + `PipeConverterViewModel` (Presentation.Wpf)|
| 2     | Carregar opções (Systems / Types / Diameters / Levels)      | `[COLLECT]`         | `PipeConverterDataLoadHandler` (Plugin Feature)                                |
| 3     | Pick em uma curva CAD (`PickObject` com filtro)             | `[SELECTION]`       | `PipeInsertionHandler.Execute` + `Common.Filters.CadCurveSelectionFilter`      |
| 4     | Resolver `Transform` (ImportInstance) ou `Identity`         | —                   | `Common/Cad/CadGeometryExtractor.GetTransformForElement`                       |
| 5     | Quebrar geometria em segmentos retos (Line/PolyLine/Arc)    | `[CALCULATE]`       | `Common/Cad/CadSegmentExtractor.ExtractSegments`                                |
| 6     | Converter mm/m em feet                                      | —                   | `Common.Units.RevitUnitConverter` (mm→feet, level+offset)                       |
| 7     | Abrir transação com `IFailuresPreprocessor`                 | `[TRANSACTION]`     | `Adapter/Features/PipeCadMapper/PipeCreator` + `Common.Transactions.FailurePreprocessors.PipeCreationFailurePreprocessor` |
| 8     | Criar placeholders (`Pipe.CreatePlaceholder`)               | `[CREATE_ELEMENT]`  | `Adapter/Features/PipeCadMapper/PipePlaceholderCreator`                         |
| 9     | Conectar extremidades coincidentes entre placeholders       | —                   | `Common.Pipes.PipeConnectorService.ConnectConsecutivePlaceholders`              |
| 10    | Converter placeholders em tubos reais                       | `[CREATE_ELEMENT]`  | `Adapter/Features/PipeCadMapper/PipePlaceholderConverter` (chama `PlumbingUtils.ConvertPipePlaceholders`) |
| 11    | Plugar pontas em tubos pré-existentes                       | —                   | `Common.Pipes.PipeConnectorService.ConnectToExistingPipes`                      |
| 12    | Compor mensagem de status                                   | `[RESULT]` `[UI]`   | `Plugin.Features.PipeCadMapper.Tools.PipeInsertionStatusFormatter`              |
| 13    | Re-armar `ExternalEvent` para próximo pick                  | —                   | `PipeInsertionHandler.RearmRequested`                                           |

## 6. Arquivos criados

### Plugin

- `Features/PipeCadMapper/PipeCadMapperButton.cs`
- `Features/PipeCadMapper/PipeCadMapperFeature.cs`
- `Features/PipeCadMapper/ShowPipeConverterCommand.cs`
- `Features/PipeCadMapper/PipeInsertionExternalEvent.cs`
- `Features/PipeCadMapper/PipeInsertionHandler.cs`
- `Features/PipeCadMapper/PipeConverterDataLoadExternalEvent.cs`
- `Features/PipeCadMapper/PipeConverterDataLoadHandler.cs`
- `Features/PipeCadMapper/Tools/PipeConversionConfigFactory.cs`
- `Features/PipeCadMapper/Tools/PipeInsertionStatusFormatter.cs`
- `Features/PipeCadMapper/Tools/RevitElementIdConversions.cs`
- `Ui/PipeConverterWindow.xaml(.cs)`

### Application

- (sem UseCase próprio — o fluxo é todo do Adapter, mediado pela Plugin
  Feature; um UseCase pode ser criado no futuro se a regra escapar do
  Revit).

### Domain

- (sem regra pura).

### Revit Adapter

- `Common/Cad/CadGeometryExtractor.cs`
- `Common/Cad/CadSegmentExtractor.cs`
- `Common/Filters/CadCurveSelectionFilter.cs`
- `Common/Pipes/PipeConnectorService.cs`
- `Common/Transactions/FailurePreprocessors/PipeCreationFailurePreprocessor.cs`
- `Features/PipeCadMapper/PipeCreator.cs`
- `Features/PipeCadMapper/PipeCreationResult.cs`
- `Features/PipeCadMapper/PipeConversionConfig.cs`
- `Features/PipeCadMapper/PipePlaceholderCreator.cs`
- `Features/PipeCadMapper/PipePlaceholderConverter.cs`

### Infrastructure

- `Infrastructure/DarivaBIM.Infrastructure.Persistence/Settings/PipeCadMapperSettings.cs` (preferências persistidas).

### Presentation

- `Presentation/DarivaBIM.Presentation.Wpf/PipeConverter/PipeConverterViewModel.cs`
- `Presentation/DarivaBIM.Presentation.Wpf/Models/{PipingSystemOptionViewModel,PipeTypeOptionViewModel,LevelOptionViewModel}.cs`

## 7. Fluxo final

```
RibbonButton "PipeCADMapper"
   → ShowPipeConverterCommand (Plugin)
   → PipeConverterWindow (Plugin Ui) + PipeConverterViewModel (Presentation)
       → PipeConverterDataLoadHandler (Plugin Feature)
            populates ViewModel via Adapter collectors
   → user picks → PipeInsertionHandler (Plugin Feature)
       → PipeConversionConfigFactory (Plugin Feature/Tools)
       → PipeCreator (Adapter Feature)
            → CadGeometryExtractor + CadSegmentExtractor (Common/Cad)
            → PipePlaceholderCreator (Adapter Feature)
            → PipeConnectorService.ConnectConsecutivePlaceholders (Common/Pipes)
            → PipePlaceholderConverter (Adapter Feature)
            → PipeConnectorService.ConnectToExistingPipes (Common/Pipes)
       → PipeInsertionStatusFormatter (Plugin Feature/Tools)
       → status na janela WPF
```

## 8. Observações

- O fluxo é modeless: o handler se reagenda
  (`RearmRequested`) e cancela picks "internos" (alteração de parâmetros no
  WPF) sem desativar a ferramenta.
- A janela usa `long` para ids; a conversão `long ↔ ElementId` mora em
  `Plugin.Features.PipeCadMapper.Tools.RevitElementIdConversions` (versionado
  porque Revit 2024+ usa `ElementId.Value`, anteriores usavam
  `ElementId.IntegerValue`).
- `IFailuresPreprocessor` suprime warnings esperados ("conexão aberta",
  "curva curta") emitidos pelo Revit durante a conversão de placeholders.

## 9. Pendências

- Suporte real a arcos (hoje convertidos como corda reta —
  `arcChordCount` apenas avisa).
- Mover `RevitElementIdConversions` para `Common/Elements/ElementIdConverter.cs`
  do Adapter quando uma segunda feature precisar converter ids.
