# ADR-0009 — Convenções de nome

## Status
Aceito.

## Decisão

### Projetos
Padrão `DarivaBIM.{Camada}[.{Sub}][.V{Versao}]`:
- `DarivaBIM.Domain`
- `DarivaBIM.Application`
- `DarivaBIM.Revit.Abstractions`
- `DarivaBIM.Revit.Hosting`
- `DarivaBIM.Revit.Adapters.V2026`
- `DarivaBIM.Plugin.V2026`
- `DarivaBIM.Presentation.Wpf`
- `DarivaBIM.Infrastructure.Api`
- `DarivaBIM.Infrastructure.Licensing`
- `DarivaBIM.Infrastructure.Persistence`
- `DarivaBIM.Infrastructure.Telemetry`

### Namespaces
Sempre iguais ao caminho da pasta (`DarivaBIM.Revit.Adapters.V2026.Writers`,
`DarivaBIM.Application.UseCases.ApplyTigreCodes`).

### Pastas dentro de cada camada
Use o nome da responsabilidade — nunca `Utils`, `Helpers`, `Manager`,
`Shared`, `Diversos`, `Outros`, `Funcoes`, `Complementos`. Preferir:
- `UseCases`, `Contracts`, `DTOs`
- `Adapters`, `Repositories`, `Readers`, `Writers`, `Mapping`, `Factories`,
  `Providers`
- `Validators`, `Filters`, `Transactions`
- `Definitions`, `Registries`, `Hosting`, `Resources`

### Classes e tipos
- UseCases: terminam com `UseCase` (`ApplyTigreCodesUseCase`).
- Contracts: começam com `I` (`ITigreCodeApplyService`).
- DTOs: nomes substantivos (`TigreCodeApplyResult`, `UnmatchedPipe`).
- Adapters: terminam com `Reader/Writer/Repository/Mapper/Factory/Provider`
  conforme o papel.
- ExternalEvent + Handler par (`PipeInsertionExternalEvent`,
  `PipeInsertionHandler`).
- Comandos Revit: terminam com `Command` (`ApplyTigreCodesCommand`).
- Definições de ribbon: terminam com `Definition`.
- Registros: terminam com `Registry`.

### Constantes e ids
- Valores estáveis para a ribbon vão no enum `RibbonCommandId`.
- IDs persistidos (Guids de `DockablePane`, `SharedParameter`) ficam em
  classes `*Definition` ou `*Ids` no projeto que os possui.

### Arquivos
Um tipo público por arquivo, salvo enum/result auxiliares pequenos vinculados
a um único tipo.

## Consequências
Quando alguém abre um diretório, vê só nomes que dizem o que cada coisa faz.
Casos de uso, contratos e DTOs ficam triviais de localizar pelo PR/tooling.
