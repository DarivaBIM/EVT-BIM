# ADR-0017 — Revit.Adapters.SharedSource materializado com namespace neutro

## Status
Aceito. Especializa o ADR-0008 (que era estritamente preventivo).

## Contexto
O ADR-0008 criou `Revit.SharedSource` como **pasta vazia** com a regra
"materializar so quando houver duplicacao real entre dois adapters". O
ADR-0015 reduziu o escopo ativo para Revit 2025 e 2026 e registrou que
"Plugin.SharedSource (ADR-0011-bis a ser escrito) passa a concentrar o
codigo antes duplicado entre V2025 e V2026" — mas a duplicacao no nivel
do adapter nunca foi resolvida.

O estado pre-ADR era:

```
src/Revit/
├── DarivaBIM.Revit.Adapters.V2025/
│   ├── Common/    (~18 arquivos com namespace ...V2025...)
│   └── Features/  (~18 arquivos com namespace ...V2025...)
└── DarivaBIM.Revit.Adapters.V2026/
    ├── Common/    (~18 arquivos com namespace ...V2026...)  ← byte-by-byte identicos a V2025
    └── Features/  (~18 arquivos com namespace ...V2026...)  ← byte-by-byte identicos a V2025
```

Diff direto entre V2025 e V2026 mostrava apenas a linha `namespace` e
`using` diferindo, mais nada. Ao mesmo tempo, `Plugin.SharedSource` tinha
9 blocos da forma:

```csharp
#if REVIT2026
using DarivaBIM.Revit.Adapters.V2026.Features.PipeCadMapper;
#elif REVIT2025
using DarivaBIM.Revit.Adapters.V2025.Features.PipeCadMapper;
#endif
```

Esses blocos so existiam por causa do namespace versionado do adapter.

## Decisao
1. **Materializar** `DarivaBIM.Revit.Adapters.SharedSource` como pasta de
   fontes compartilhadas (mesma politica do `Plugin.SharedSource` —
   ADR-0011 + ADR-0015).

2. **Namespace neutro**: codigo da SharedSource fica em
   `DarivaBIM.Revit.Adapters.*` (sem versao). Os tipos resolvidos em
   tempo de build sao os mesmos para V2025 e V2026; o que muda e contra
   qual `RevitAPI.dll` cada um e compilado.

3. **Cascas versionadas**: `DarivaBIM.Revit.Adapters.V2025` e
   `DarivaBIM.Revit.Adapters.V2026` permanecem como projetos separados
   contendo apenas o `.csproj`. Cada um:
   - declara seu proprio `<RevitVersion>`;
   - puxa toda SharedSource via `<Compile Include>` com `Link`;
   - mantem seu `AssemblyName` versionado (`DarivaBIM.Revit.Adapters.V2025.dll`
     e `DarivaBIM.Revit.Adapters.V2026.dll`).

4. **Diferencas reais de RevitAPI** entre versoes (cenario futuro 2027/2028)
   sao tratadas com `#if REVITxxxx` localizado dentro da SharedSource,
   nao com namespaces versionados. Os simbolos `REVIT2025` e `REVIT2026`
   ja sao definidos automaticamente em
   `src/Build/Directory.Build.targets` a partir de `<RevitVersion>`.

5. **Arquitetura preservada por testes**:
   - `LayerIsolationTests.AdapterSharedSource_does_not_reference_versioned_namespaces`
     (novo): proibe `using DarivaBIM.Revit.Adapters.V2025/V2026` e
     `using DarivaBIM.Plugin.V2025/V2026` em qualquer arquivo da
     SharedSource;
   - `LayerIsolationTests.PluginSharedSource_does_not_reference_versioned_namespaces`
     foi expandido para incluir tambem os namespaces versionados do adapter.

## Mudancas concretas
- Renomeada a pasta `DarivaBIM.Revit.SharedSource` -> `DarivaBIM.Revit.Adapters.SharedSource`
- 36 arquivos `.cs` movidos do V2026 para SharedSource (com `git mv`,
  preservando historico)
- 36 copias byte-by-byte do V2025 apagadas
- Todos os namespaces nos arquivos movidos: `DarivaBIM.Revit.Adapters.V2026.*`
  -> `DarivaBIM.Revit.Adapters.*`
- `Adapters.V2025.csproj` e `Adapters.V2026.csproj`: viraram cascas com
  `<Compile Include>` da SharedSource
- 9 blocos `#if REVIT2026/#elif REVIT2025` removidos do `Plugin.SharedSource`,
  substituidos por um unico `using DarivaBIM.Revit.Adapters.*`
- `RevitAdapterServiceRegistration.cs` (V2025 e V2026): xmldoc atualizado
  para namespace neutro

## Consequencias positivas
- Para escrever uma feature nova em adapter, escrevo em **um** lugar
  (`Adapters.SharedSource/Features/<Feature>/...`) e ela compila para as
  duas versoes automaticamente
- 36 arquivos deduplicados, ~2500 LOC removidos
- 9 blocos `#if` desnecessarios removidos do Plugin.SharedSource
- Adicionar Revit 2027/2028 no futuro: criar mais uma casca
  `Adapters.V2027.csproj` com apenas `<RevitVersion>` e `<Compile Include>`,
  aplicar `#if REVIT2027` onde houver divergencia real

## Consequencias negativas
- Diferencas reais de API entre 2025 e 2026 (caso futuro) virao em forma
  de `#if`. Risco mitigado: o padrao ja e usado e validado no
  `Plugin.SharedSource`, e os testes de fronteira asseguram que no
  caminho normal o codigo permanece neutro.
- Quem buscar "Adapters.V2026" no Solution Explorer vai encontrar so o
  csproj. Mitigacao: README na pasta SharedSource explica a estrutura.

## Reversibilidade
Reverter envolve duplicar a SharedSource de volta para V2025 e V2026 com
namespaces versionados, recolocar os `#if` no Plugin.SharedSource e
ajustar os testes. E mecanico mas trabalhoso. Como o objetivo e justamente
eliminar a duplicacao, nao se espera reverter — apenas evoluir.

## Relacao com outros ADRs
- **Especializa ADR-0008**: a politica "nao criar SharedSource
  proativamente" continua valendo para casos sem duplicacao. Aqui
  havia duplicacao byte-by-byte entre dois adapters, o que satisfaz
  exatamente o gatilho previsto pelo ADR-0008.
- **Encerra a pendencia do ADR-0015**: o "ADR-0011-bis" mencionado no
  ADR-0015 como pendente passa a ser atendido em duas frentes —
  `Plugin.SharedSource` (ja existia) e `Adapters.SharedSource` (este
  ADR).
- **Reforca ADR-0011** (plugin features organization): a mesma logica
  de "feature em um lugar so, neutralidade entre versoes" agora vale
  tambem no nivel adapter.
