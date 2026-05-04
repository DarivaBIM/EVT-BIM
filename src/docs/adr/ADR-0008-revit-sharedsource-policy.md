# ADR-0008 — Política para `Revit.SharedSource`

## Status
Aceito. **Materializado pelo ADR-0017** (a pasta foi renomeada para
`Revit.Adapters.SharedSource` e populada com 37 arquivos antes
duplicados entre V2025 e V2026). A politica abaixo continua valendo
para *novos* candidatos: nao criar SharedSource proativamente, so
quando houver duplicacao real.

## Contexto
`DarivaBIM.Revit.SharedSource` é uma pasta destinada a **compartilhar
arquivos-fonte** (não DLLs) entre adapters de versões diferentes do Revit. Ele
seria materializado como um Shared Project (`.shproj`) ou via
`<Compile Include="..\..\SharedSource\..\*.cs" Link="..." />` em cada
adapter.

A diferença para um `Common.dll` tradicional é que cada adapter compila o
mesmo código com sua própria versão da RevitAPI. Isso evita a armadilha de
publicar um `Revit.Common.dll` amarrado a uma versão específica da API.

## Decisão
1. **Não criar** `SharedSource` proativamente. A pasta existe e está vazia.
2. Materializar **somente** quando houver duplicação real entre dois
   adapters — por exemplo, quando V2023 e V2026 tiverem um `TransactionRunner`
   identico.
3. **Proibir** colocar regra de negócio em `SharedSource`. Só vai código que
   é genuinamente Revit-mecânico (ex.: `RibbonBuilder` interno, helpers de
   parâmetro).
4. **Proibir** publicar `Revit.Common.dll`. Sempre compilar como source-shared.

## Candidatos legítimos (futuros)
- `TransactionRunner`
- `SelectionService`
- `ParameterReader` / `ParameterWriter`
- `ExternalEventQueue`
- `RibbonBuilder` (caso passe a ser per-version por causa de quebra de API)
- `IconProvider`

## Consequências
- Evitamos abstração prematura.
- Quando aparecer duplicação real, a migração é mecânica.
- Diff entre adapters tende a refletir **só** as mudanças reais de API entre
  versões.
