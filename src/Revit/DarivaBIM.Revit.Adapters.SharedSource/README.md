# DarivaBIM.Revit.Adapters.SharedSource

Pasta de **arquivos-fonte compartilhados** entre adapters de diferentes
versoes do Revit (`DarivaBIM.Revit.Adapters.V2025`,
`DarivaBIM.Revit.Adapters.V2026`).

## Politica

Esta pasta **nao tem `.csproj` proprio** e **nao gera DLL**. Cada adapter
versionado inclui os arquivos daqui via `MSBuild Link` no seu proprio
`.csproj`, compilando-os contra a `RevitAPI.dll` da versao correspondente.
Mesmo padrao do `Plugin.SharedSource` (ADR-0011).

Principio (ADR-0008 + ADR-0017):

- Compartilhar **fonte**, nao DLL — porque cada Revit (2025/2026) tem
  sua propria `RevitAPI.dll` e uma DLL pre-compilada contra uma versao
  pode falhar em outra.
- Cada `Adapters.V20XX` continua produzindo seu proprio assembly final
  com nome e identidade independentes.
- Namespace **neutro** (`DarivaBIM.Revit.Adapters.*`, sem versao).
  Diferencas de RevitAPI entre versoes sao tratadas com `#if REVITxxxx`
  localizado, nao com namespaces versionados.

## Estrutura

```
DarivaBIM.Revit.Adapters.SharedSource/
├── Common/
│   ├── Cad/
│   ├── Filters/
│   ├── Parameters/
│   ├── Pipes/
│   ├── SharedParameters/
│   ├── Transactions/
│   └── Units/
└── Features/
    ├── FamiliesImporter/
    ├── ParameterEditor/
    ├── PipeCadMapper/
    ├── Prolongador/
    └── TigreCodes/
```

## Diferencas de RevitAPI entre versoes

| Tipo de diferenca                          | Como tratar                                     |
|--------------------------------------------|-------------------------------------------------|
| API simetrica (caso 95%)                   | Codigo unico, sem `#if`                         |
| Mesmo metodo, assinatura diferente         | `#if REVIT2025` / `#elif REVIT2026` no trecho   |
| Tipo novo so existe em 2026+               | Arquivo inteiro com `#if REVIT2026`             |
| Comportamento radicalmente diferente       | Interface neutra + arquivos por versao com `#if`|

Os simbolos `REVIT2025` e `REVIT2026` sao definidos automaticamente em
`src/Build/Directory.Build.targets` a partir do `RevitVersion` do csproj.
