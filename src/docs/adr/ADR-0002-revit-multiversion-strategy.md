# ADR-0002 — Estratégia multi-versão Revit (2023–2027)

## Status
Aceito.

## Contexto
O plugin precisa rodar em Revit 2023, 2024, 2025, 2026 e 2027. Cada versão da
RevitAPI tem **breaking changes** sutis (ex.: `ElementId.Value` é `long` no
Revit 2024+, era `int` antes; `ForgeTypeId` mudou em 2022; o runtime saltou de
.NET Framework 4.8 para .NET 8 no Revit 2025; previsão de .NET 10 no Revit
2027).

## Decisão
Usar **um Adapter e um Plugin por versão**:

| Versão | Runtime           | Adapter                          | Plugin                          |
|--------|-------------------|----------------------------------|---------------------------------|
| 2023   | net48             | `DarivaBIM.Revit.Adapters.V2023` | `DarivaBIM.Plugin.V2023`        |
| 2024   | net48             | `DarivaBIM.Revit.Adapters.V2024` | `DarivaBIM.Plugin.V2024`        |
| 2025   | net8.0-windows    | `DarivaBIM.Revit.Adapters.V2025` | `DarivaBIM.Plugin.V2025`        |
| 2026   | net8.0-windows    | `DarivaBIM.Revit.Adapters.V2026` | `DarivaBIM.Plugin.V2026`        |
| 2027   | net10.0-windows   | `DarivaBIM.Revit.Adapters.V2027` | `DarivaBIM.Plugin.V2027`        |

Cada projeto declara `<RevitVersion>` no `.csproj`. O
`src/Build/Directory.Build.targets` injeta as referências corretas
(`RevitAPI.dll`, `RevitAPIUI.dll`) para a versão.

`Domain`, `Application` e `Revit.Abstractions` usam **`netstandard2.0`** para
serem consumíveis tanto pelos plugins net48 (V2023/V2024) quanto pelos plugins
net8/net10.

## Status atual
- V2026 é o alvo de produção e contém todo o código real.
- V2023, V2024, V2025 e V2027 são **stubs** (`.csproj` + `Placeholder.cs`).
- Quando uma versão for materializada, copia-se o V2026 para o adapter da
  versão e ajusta-se o que mudou na API. Código realmente compartilhável vai
  para `DarivaBIM.Revit.SharedSource` (ver ADR-0008).

## Consequências
- O caminho de instalação e o assembly do `.addin` são por versão
  (`%ProgramData%\Autodesk\Revit\Addins\<2026>\DarivaBIM\DarivaBIM.Plugin.V2026.dll`).
- `Hosting` hoje é single-target net8.0-windows + RevitAPI 2026; será
  multitarget quando V2023/V2024 forem materializados (ADR-0007).
