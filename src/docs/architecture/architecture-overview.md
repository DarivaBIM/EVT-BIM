# DarivaBIM — Visão geral da arquitetura

## 1. Camadas e dependências

```
┌──────────────────────────────────────────────────────────────────┐
│                        Plugin.Vxxxx                             │
│         (IExternalApplication, IExternalCommand, .addin)        │
│         ▲ ▲                                          ▲          │
│         │ └── refs ── Presentation.Wpf               │          │
│         └── refs ── Revit.Adapters.Vxxxx ─── refs ── Hosting    │
└────────────────────────────────────┬─────────────────────────────┘
                                     │
              refs                   ▼
┌──────────────────────────────────────────────────────────────────┐
│                   Revit.Abstractions  +  Hosting                 │
│   (interfaces neutras + DI + ExternalEvent/Idling/Ribbon)        │
└──────────────────────────────────────────────────────────────────┘
                                     ▲
                                     │
                ┌────────────────────┴──────────────────┐
                │              Application              │
                │ (UseCases, Contracts, DTOs)           │
                └────────────────────┬──────────────────┘
                                     ▼
                ┌──────────────────────────────────────┐
                │                Domain                │
                │ (entidades, VOs, regras puras)       │
                └──────────────────────────────────────┘
```

Direção das setas = "depende de". Tudo aponta para baixo. Nada da camada
inferior conhece a superior.

## 2. Regras de não-vazamento da RevitAPI

Os tipos `Document`, `Element`, `ElementId`, `Connector`, `FamilyInstance`,
`BuiltInParameter`, `Transaction`, `UIApplication`, `UIDocument`,
`TaskDialog` **não podem** aparecer em:
- `DarivaBIM.Domain`
- `DarivaBIM.Application`
- `DarivaBIM.Presentation.Wpf`

Eles só podem aparecer em:
- `DarivaBIM.Revit.Adapters.Vxxxx`
- `DarivaBIM.Plugin.Vxxxx`
- `DarivaBIM.Revit.Hosting`

`Revit.Abstractions` define interfaces neutras (`IRevitDocumentContext`,
`IRevitTransactionRunner`, `IRevitParameterWriter`, `IRevitSelectionService`)
que substituem esses tipos quando o Core precisa falar com o host.

## 3. Fluxo de uma ferramenta

```
Usuário clica botão da Ribbon
    │
    ▼
Revit instancia a IExternalCommand (ex.: ApplyTigreCodesCommand)
    │
    ▼
Command.Execute → App.Executor.Execute(commandData, ref message, ctx => ...)
    │
    ├── using IServiceScope = host.CreateScope()
    │
    ├── new RevitCommandContext(commandData, scope.ServiceProvider, RevitDocumentContext(doc))
    │
    └── action(ctx):
        ├── resolve dependências do ctx.Services
        ├── instancia o Adapter Revit concreto (ex.: TigreCodeApplier)
        ├── instancia o UseCase (ex.: ApplyTigreCodesUseCase)
        ├── chama useCase.Execute()
        │       │
        │       └── usa ITigreCatalogProvider (Domain), Domain.TigreCatalog
        │           e a interface ITigreCodeApplyService (Application)
        │
        └── retorna Result.Succeeded / Failed / Cancelled
```

## 4. Organização da Ribbon

```
RibbonDefinition
└── RibbonPanelDefinition
    └── RibbonButtonDefinition (commandId: RibbonCommandId.WriteTigreCodes)
                                       │
                                       ▼
                            ICommandRegistry (Plugin.Vxxxx)
                                       │
                                       ▼
                            typeof(ApplyTigreCodesCommand)
```

Adicionar uma ferramenta nova significa:
1. Criar a classe `IExternalCommand`.
2. Acrescentar um valor em `RibbonCommandId` (se for novo conceito).
3. Mapear o `RibbonCommandId` no `CommandRegistry`.
4. Listar a ferramenta em `DarivaBimRibbonDefinition`.

Sem mudar `App.cs`, sem mexer em `RibbonBuilder`.

## 5. Multi-versão Revit

| Versão | TFM             | Status atual |
|--------|-----------------|--------------|
| 2023   | net48           | Stub (csproj + Placeholder.cs) |
| 2024   | net48           | Stub |
| 2025   | net8.0-windows  | Stub |
| 2026   | net8.0-windows  | **Produção** |
| 2027   | net10.0-windows | Stub |

`Domain`, `Application`, `Revit.Abstractions` e `Infrastructure.*` usam
`netstandard2.0` para serem consumíveis pelos plugins net48, net8 e net10.

`Hosting`, `Adapters.Vxxxx` e `Plugin.Vxxxx` usam o TFM da versão.

## 6. Como adicionar uma nova versão do Revit

1. Criar `src/Revit/DarivaBIM.Revit.Adapters.V20XX/` copiando o V2026.
2. Criar `src/Plugins/DarivaBIM.Plugin.V20XX/` copiando o V2026.
3. Trocar `<RevitVersion>2026</RevitVersion>` para `<RevitVersion>20XX</RevitVersion>`
   nos dois csprojs.
4. Ajustar TFM se necessário (`net48` para 2023/2024, `net10.0-windows` para
   2027+).
5. Criar `src/Build/AddinManifests/DarivaBIM.V20XX.addin`.
6. Adicionar os 2 projetos novos no `DarivaBIM.sln`.
7. Quebrar o que mudou na API; idealmente os tests do Adapter pegam.

## 7. Como adicionar uma nova ferramenta/botão

1. Definir um valor novo em `RibbonCommandId` se a ação não existir.
2. Criar `MeuComandoCommand : IExternalCommand` em
   `DarivaBIM.Plugin.V2026/Commands/`.
3. Mapear o `RibbonCommandId → typeof(MeuComandoCommand)` em `CommandRegistry`.
4. Adicionar `RibbonButtonDefinition` em `DarivaBimRibbonDefinition`.
5. Se a ferramenta tem regra de negócio: criar UseCase em
   `DarivaBIM.Application.UseCases.MinhaFuncionalidade/` que recebe interfaces
   por construtor.
6. Implementar as interfaces em `DarivaBIM.Revit.Adapters.V2026/...` ou
   `DarivaBIM.Infrastructure.*`.

## 8. Como criar um novo UseCase

1. `DarivaBIM.Application/UseCases/<Nome>/<Nome>UseCase.cs`.
2. Constructor recebe interfaces (`I*Service`, `I*Repository`, `I*Provider`).
3. Método `Execute(...)` retorna DTOs (`*Result`).
4. Sem qualquer `Autodesk.Revit.*`, `WPF` ou `HttpClient`.
5. Teste em `DarivaBIM.Application.Tests` com fakes/in-memory.

## 9. Como criar um novo adapter Revit

1. Em `DarivaBIM.Revit.Adapters.V2026/<Categoria>/<Nome>.cs`.
2. Implementa o `I*Service`/`I*Repository` definido em Application.
3. Recebe `Document` (ou `IRevitDocumentContext`) por construtor.
4. Encapsula transações, leitura/escrita de parâmetros, conversão de unidades,
   filtros de categoria.
5. Testar com smoke tests em `DarivaBIM.Revit.Adapters.V2026.Tests`.

## 10. Como compilar e testar V2026

```cmd
:: Pré-requisito: Revit 2026 instalado em "C:\Program Files\Autodesk\Revit 2026"
:: ou variável de ambiente REVIT_2026_PATH apontando para a instalação.

dotnet restore DarivaBIM.sln
dotnet build src\Plugins\DarivaBIM.Plugin.V2026\DarivaBIM.Plugin.V2026.csproj -c Debug
```

O target `DeployAddin` (no csproj do Plugin.V2026) chama
`build/deploy_revit_2026.cmd`, que copia o `.addin` para
`%ProgramData%\Autodesk\Revit\Addins\2026\` e os assemblies para a subpasta
`DarivaBIM\`.

Testes unitários (Domain + Application + Abstractions) podem rodar em
qualquer máquina Windows com .NET 8 SDK:
```cmd
dotnet test src\tests\DarivaBIM.Domain.Tests
dotnet test src\tests\DarivaBIM.Application.Tests
dotnet test src\tests\DarivaBIM.Revit.Abstractions.Tests
```

Testes de adapter exigem a RevitAPI e só rodam em máquinas com Revit
instalado.
