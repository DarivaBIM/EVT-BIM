# Migração Dynamo → Plugin: NomeDaFerramenta

> Substitua "NomeDaFerramenta" pelo nome real e remova este bloco quando o
> documento estiver pronto. Veja
> `src/docs/guides/dynamo-to-plugin-migration.md` para o procedimento
> completo.

## 1. Objetivo da ferramenta

Descrever em uma frase o que a ferramenta faz, do ponto de vista do
usuário (não técnico).

## 2. Script original

- **Dynamo (.dyn)**:
- **Python (.py)**:
- **Data**:
- **Autor original**:

## 3. Entradas

| Entrada no Dynamo | Tipo (Dynamo)   | Vai para                    |
|-------------------|------------------|-----------------------------|
|                   |                  |                             |

## 4. Saídas

| Saída             | Tipo             | Vai para                    |
|-------------------|------------------|-----------------------------|
|                   |                  |                             |

## 5. Etapas do script original

Numere as linhas/blocos do script e classifique cada um com uma etiqueta.

| Ordem | Etapa (resumo)             | Etiqueta            | Destino no plugin                                     |
|-----:|-----------------------------|---------------------|-------------------------------------------------------|
| 1     | …                          | `[INPUT]`           | …                                                     |
| 2     | …                          | `[SELECTION]`       | …                                                     |
| 3     | …                          | `[COLLECT]`         | …                                                     |
| 4     | …                          | `[READ_PARAM]`      | …                                                     |
| 5     | …                          | `[FILTER]`          | …                                                     |
| 6     | …                          | `[CALCULATE]`       | …                                                     |
| 7     | …                          | `[TRANSACTION]`     | …                                                     |
| 8     | …                          | `[WRITE_PARAM]`     | …                                                     |
| 9     | …                          | `[CREATE_ELEMENT]`  | …                                                     |
| 10    | …                          | `[RESULT]`          | …                                                     |
| 11    | …                          | `[UI]`              | …                                                     |

Etiquetas válidas: `[INPUT]`, `[SELECTION]`, `[COLLECT]`, `[READ_PARAM]`,
`[FILTER]`, `[CALCULATE]`, `[WRITE_PARAM]`, `[CREATE_ELEMENT]`,
`[TRANSACTION]`, `[RESULT]`, `[UI]`.

## 6. Arquivos criados

### Plugin (`src/Plugins/DarivaBIM.Plugin.V2026/Features/<Nome>/`)

- `<Nome>Button.cs`
- `<Nome>Feature.cs`
- `<Nome>Command.cs`
- `<Nome>Tool.cs` (opcional)
- `<Nome>ExternalEvent.cs` / `<Nome>Handler.cs` (se modeless)
- `Tools/...` (helpers do Plugin específicos da feature)

### Application (`src/Core/DarivaBIM.Application/`)

- `UseCases/<Nome>/<Nome>UseCase.cs` (se houver orquestração)
- `Contracts/I<Nome>Service.cs` (interfaces requeridas pelo UseCase)
- `DTOs/<Nome>/<Nome>Result.cs` (resultados/saídas neutras)

### Domain (`src/Core/DarivaBIM.Domain/`)

- Entidades / value objects / regras puras (se houver cálculo puro).

### Revit Adapter (`src/Revit/DarivaBIM.Revit.Adapters.V2026/Features/<Nome>/`)

- `<Nome>Collector.cs` (coletores)
- `<Nome>Reader.cs` (leitura de parâmetros do projeto)
- `<Nome>Writer.cs` ou `<Nome>Creator.cs` (escrita/criação)
- `<Nome>Resolver.cs` (resolução de Level/Type/SystemType)
- `SharedParameters/<Nome>SharedParameters.cs` (se a feature usa shared parameter)

### Infrastructure (`src/Infrastructure/...`)

- Loaders, persistência local, clientes HTTP, telemetria — se aplicável.

### Presentation (`src/Presentation/DarivaBIM.Presentation.Wpf/`)

- View / ViewModel neutros (sem RevitAPI), se houver janela.

## 7. Fluxo final

```
Botão da Ribbon  →  IExternalCommand (Plugin Feature)
                 →  Tool (Plugin Feature) [resolvida via DI]
                 →  UseCase (Application)
                 →  Adapter Feature (Collector/Reader/Writer/Creator)
                 →  Common (SharedParameters / Parameters / Units / Transactions)
                 →  Domain (regras puras), se houver
                 →  Result DTO de volta até o Plugin
                 →  TaskDialog / status na UI
```

## 8. Observações

- Pontos sensíveis do script original (gambiarras conhecidas, dependências
  de versão do Revit, comportamento que NÃO deve mudar).

## 9. Pendências

- Itens deixados para uma próxima leva (extrações, refactors maiores,
  testes).
