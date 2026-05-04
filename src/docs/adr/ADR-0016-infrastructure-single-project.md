# ADR-0016 — Infrastructure como projeto unico

## Status
Aceito.

## Contexto
Antes deste ADR, a camada de infraestrutura era dividida em quatro projetos
separados, cada um com seu proprio `.csproj`:

| Projeto                              | Conteudo real                              |
| ------------------------------------ | ------------------------------------------ |
| `DarivaBIM.Infrastructure.Api`       | `ApiClient`, `FamilyDownloadService` (2)   |
| `DarivaBIM.Infrastructure.Persistence` | Cache, Settings, TigreCatalog (3)        |
| `DarivaBIM.Infrastructure.Licensing` | `Placeholder.cs` ("Future home...")        |
| `DarivaBIM.Infrastructure.Telemetry` | `Placeholder.cs` ("Future home...")        |

Dois desses csprojs eram literalmente placeholders sem codigo. Os outros
dois eram sempre consumidos juntos por todo plugin (V2025 e V2026 puxavam
os quatro), e nao havia cenario realista de empacotar um deles como NuGet
independente.

A separacao em quatro csprojs cobrava um custo concreto:

- 4 nodes a mais no Solution Explorer
- 4 ProjectReferences em cada plugin (V2025 e V2026 = 8 totais)
- compreender a estrutura exigia abrir os 4 csprojs para descobrir que
  dois eram vazios
- nao ha consumidor externo da fronteira binaria

## Decisao
1. Consolidar os quatro projetos em um unico `DarivaBIM.Infrastructure`
   com pastas internas que preservam a estrutura logica:

   ```
   DarivaBIM.Infrastructure/
   ├── Api/Clients/
   ├── Persistence/Cache/
   ├── Persistence/Settings/
   ├── Persistence/TigreCatalog/
   ├── Licensing/    (reservada, com README)
   └── Telemetry/    (reservada, com README)
   ```

2. Os namespaces atuais (`DarivaBIM.Infrastructure.Api.Clients`,
   `.Persistence.Cache` etc.) sao **preservados**, entao nenhum `using`
   nos consumidores precisa mudar.

3. A fronteira logica `Persistence/` ↔ `Api/` ↔ `Licensing/` ↔ `Telemetry/`,
   antes garantida pela ausencia de `ProjectReference`, agora e garantida
   por:
   - convencao de pastas + namespaces;
   - `InfrastructureBoundariesTests` em `DarivaBIM.Architecture.Tests`,
     que falha o build se `Persistence/` usar `System.Net.Http` ou se
     `Licensing/Telemetry` referenciar outras pastas Infra.

4. As pastas `Licensing/` e `Telemetry/` sao mantidas como reservas
   nominais (com README explicativo), porque o tema delas e
   suficientemente isolado para evoluir em paralelo.

## Implicacoes
- **Plugin.V2025/V2026**: 4 ProjectReferences -> 1
- **Adapters.V2025/V2026**: ProjectReference para Infrastructure
  (substituindo `Infrastructure.Persistence`)
- **DarivaBIM.sln**: -3 entradas de projeto
- **DarivaBIM.*.slnf**: idem
- ArchTest novo (`InfrastructureBoundariesTests`): 4 testes
- Total de projetos: -3 (de 19 para 16)

## Consequencias positivas
- Solution Explorer reduzido em 3 nodes
- Quem abre a Infrastructure pela primeira vez ve a estrutura logica
  em uma unica pasta, em vez de quatro pastas com `Placeholder.cs`
- Compilacao marginalmente mais rapida (1 csproj vs 4)
- `ProjectReference` dos plugins fica trivial

## Consequencias negativas
- A fronteira `Persistence` nao usa HTTP passa a depender de teste de
  arquitetura. Risco mitigado pelo `InfrastructureBoundariesTests`:
  qualquer regressao quebra o build em CI.
- Empacotar uma das partes como NuGet independente exige reverter
  parcialmente este ADR. Trade-off aceito porque nao ha demanda real.

## Reversibilidade
Reverter este ADR significa recriar 1-3 csprojs e mover arquivos de volta.
Os testes de arquitetura podem permanecer mesmo apos a separacao, como
defesa adicional.

## Relacao com outros ADRs
- Reforca ADR-0001 (clean architecture, core-agnostic): a fronteira
  arquitetural continua valida, so muda o mecanismo de enforcement.
- Nao afeta ADR-0007 (Revit hosting layer): Infrastructure nunca foi
  Revit-aware.
