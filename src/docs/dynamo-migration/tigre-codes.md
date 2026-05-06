# Migração Dynamo → Plugin: PipeCodes (Códigos Tigre)

## 1. Objetivo da ferramenta

Garantir o shared parameter `Tigre: Código` em todos os tubos do projeto e
preencher o código numérico do catálogo Tigre que casa com a descrição,
segmento, tipo e diâmetro de cada tubo.

## 2. Script original

- **Dynamo (.dyn)**: roteiro Python embutido em um nó Code Block do
  `Tigre — Aplicar códigos.dyn` (interno).
- **Python (.py)**: lógica de criação do shared parameter + iteração nos
  tubos + lookup no catálogo.
- **Data**: anterior à versão 0.1 do plugin.

## 3. Entradas

| Entrada no Dynamo                    | Tipo               | Vai para                                     |
|--------------------------------------|--------------------|----------------------------------------------|
| Botão "Run" do Dynamo Player         | trigger            | `RibbonButton` no Plugin Feature             |
| Nome/GUID do shared parameter        | constantes         | `Domain.Tigre.TigreSharedParameterDefinition` (puro) + `Adapter.Features.TigreCodes.SharedParameters.TigreCodesSharedParameters` (com RevitAPI) |
| Catálogo Tigre (CSV embutido)        | recurso             | `Resources/tigre_codes.json` + `Infrastructure.Persistence.TigreCatalog.TigreCatalogJsonLoader` |

## 4. Saídas

| Saída                                | Tipo               | Vai para                                     |
|--------------------------------------|--------------------|----------------------------------------------|
| Quantos tubos foram atualizados      | int                | `Application.DTOs.Tigre.TigreCodeApplyResult.PipesUpdated` |
| Lista de tubos sem match no catálogo | list[UnmatchedPipe] | `TigreCodeApplyResult.Unmatched`             |
| Mensagem para o usuário              | TaskDialog          | `ApplyTigreCodesUseCase.FormatReport` + `TaskDialog` em `ApplyPipeCodesTool` |

## 5. Etapas do script original

| Ordem | Etapa                                                       | Etiqueta            | Destino no plugin                                                              |
|-----:|-------------------------------------------------------------|---------------------|--------------------------------------------------------------------------------|
| 1     | Carregar catálogo Tigre (CSV/JSON)                          | `[INPUT]`           | `Infrastructure.Persistence.TigreCatalog.TigreCatalogJsonLoader`               |
| 2     | Abrir transação                                             | `[TRANSACTION]`     | `Adapter/Features/TigreCodes/TigreCodeApplier` (`ExecuteInWriteTransaction`)   |
| 3     | Garantir shared parameter `Tigre: Código`                   | —                   | `Common.SharedParameters.SharedParameterService.Ensure(doc, TigreCodesSharedParameters.Code)` |
| 4     | `Document.Regenerate()` para o binding ficar visível        | —                   | `TigreCodeApplier.Apply()`                                                     |
| 5     | Coletar todos os tubos (`OST_PipeCurves`)                   | `[COLLECT]`         | `Adapter/Features/TigreCodes/TigrePipeCollector`                               |
| 6     | Para cada tubo, ler descrição (com fallback)                | `[READ_PARAM]`      | `Adapter/Features/TigreCodes/TigrePipeDataReader.GetPipeDescriptionText`       |
| 7     | Ler segmento (`RBS_PIPE_SEGMENT_PARAM`)                     | `[READ_PARAM]`      | `TigrePipeDataReader.Read`                                                     |
| 8     | Ler nome do tipo                                            | `[READ_PARAM]`      | `TigrePipeDataReader.GetPipeTypeName`                                          |
| 9     | Ler diâmetro nominal e converter feet→mm                    | `[READ_PARAM]`      | `TigrePipeDataReader.GetPipeDiameterMm` + `Common.Units.RevitUnitConverter`     |
| 10    | Normalizar texto e procurar match no catálogo               | `[CALCULATE]`       | `Domain.Tigre.TigreCatalog.FindMatch` + `TigreTextUtils.Normalize`             |
| 11    | Buscar parâmetro alvo no tubo (por nome → GUID)             | —                   | `Common.SharedParameters.SharedParameterService.GetParameter`                   |
| 12    | Escrever código (Integer/String) e atualizar contadores     | `[WRITE_PARAM]`     | `Adapter/Features/TigreCodes/TigreCodeWriter`                                  |
| 13    | Registrar tubos sem match                                   | `[RESULT]`          | `TigreCodeApplier.RegisterNoMatch` + `Application.DTOs.Tigre.UnmatchedPipe`     |
| 14    | Compor relatório                                            | `[RESULT]`          | `Application.DTOs.Tigre.TigreCodeApplyResult` + `ApplyTigreCodesUseCase.FormatReport` |
| 15    | Mostrar TaskDialog                                          | `[UI]`              | `Plugin.Features.PipeCodes.ApplyPipeCodesTool.Execute`                          |

## 6. Arquivos criados

### Plugin

- `Plugins/DarivaBIM.Plugin.V2026/Features/PipeCodes/PipeCodesButton.cs`
- `Plugins/DarivaBIM.Plugin.V2026/Features/PipeCodes/PipeCodesFeature.cs`
- `Plugins/DarivaBIM.Plugin.V2026/Features/PipeCodes/ApplyPipeCodesCommand.cs`
- `Plugins/DarivaBIM.Plugin.V2026/Features/PipeCodes/ApplyPipeCodesTool.cs`

### Application

- `Core/DarivaBIM.Application/Contracts/ITigreCodeApplyService.cs`
- `Core/DarivaBIM.Application/Contracts/ITigreCatalogProvider.cs`
- `Core/DarivaBIM.Application/UseCases/ApplyTigreCodes/ApplyTigreCodesUseCase.cs`
- `Core/DarivaBIM.Application/DTOs/Tigre/TigreCodeApplyResult.cs`
- `Core/DarivaBIM.Application/DTOs/Tigre/UnmatchedPipe.cs`

### Domain

- `Core/DarivaBIM.Domain/Tigre/TigreCatalog.cs`
- `Core/DarivaBIM.Domain/Tigre/TigreCatalogEntry.cs`
- `Core/DarivaBIM.Domain/Tigre/TigreTextUtils.cs`
- `Core/DarivaBIM.Domain/Tigre/TigreSharedParameterDefinition.cs`

### Revit Adapter

- `Revit/DarivaBIM.Revit.Adapters.V2026/Features/TigreCodes/TigreCodeApplier.cs`
- `Revit/DarivaBIM.Revit.Adapters.V2026/Features/TigreCodes/TigrePipeCollector.cs`
- `Revit/DarivaBIM.Revit.Adapters.V2026/Features/TigreCodes/TigrePipeDataReader.cs`
- `Revit/DarivaBIM.Revit.Adapters.V2026/Features/TigreCodes/TigreCodeWriter.cs`
- `Revit/DarivaBIM.Revit.Adapters.V2026/Features/TigreCodes/SharedParameters/TigreCodesSharedParameters.cs`

### Infrastructure

- `Infrastructure/DarivaBIM.Infrastructure.Persistence/TigreCatalog/TigreCatalogJsonLoader.cs`
- `Plugins/DarivaBIM.Plugin.V2026/Resources/tigre_codes.json`

### Presentation

- (não há janela WPF — feature roda direto via TaskDialog).

## 7. Fluxo final

```
RibbonButton "Codificar Tubos"
   → ApplyPipeCodesCommand (Plugin Feature, casca fina)
   → ApplyPipeCodesTool (Plugin Feature, resolvido via DI)
       → ApplyTigreCodesUseCase (Application)
           → TigreCodeApplier (Adapter Feature, implementa ITigreCodeApplyService)
               → SharedParameterService.Ensure (Common)
               → TigrePipeCollector + TigrePipeDataReader (Adapter Feature)
               → TigreCatalog.FindMatch (Domain)
               → SharedParameterService.GetParameter (Common)
               → TigreCodeWriter (Adapter Feature)
       ← TigreCodeApplyResult (Application DTO)
   ← TaskDialog formatado
```

## 8. Observações

- O parâmetro existente com mesmo nome mas GUID diferente é
  **propositalmente** aproveitado (replica o Dynamo): o serviço genérico só
  emite um `Warning`. Isso evita pedir ao usuário para apagar parâmetros de
  templates antigos.
- A escolha entre `Integer` e `String` para o storage do parâmetro é
  decidida no Revit pelo `ExternalDefinitionCreationOptions(SpecTypeId.Int.Integer)`
  — o `TigreCodeWriter` cobre ambos para suportar projetos legados.

## 9. Pendências

- Cobrir `TigrePipeDataReader` com testes baseados em fakes do
  `Document`/`Pipe` quando uma camada de testes para Adapter for criada.
- Avaliar mover `ParameterTextReader` para reuso direto pelo
  `RevitParameterReader.AsText(...)` sem chamar dois nomes diferentes.
