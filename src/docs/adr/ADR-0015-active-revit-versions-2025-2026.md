# ADR-0015 — Escopo ativo: Revit 2025 e 2026

## Status
Aceito. Substitui (parcialmente) o ADR-0002.

## Contexto
O ADR-0002 previa suporte simultâneo a Revit 2023, 2024, 2025, 2026 e 2027.
Na prática, V2023, V2024 e V2027 nunca saíram da fase de stub
(`.csproj` + `Placeholder.cs`), enquanto V2025 e V2026 evoluíram em paralelo
com duplicação massiva de código entre si (Features, Ribbon, Ui, Resources).

Manter os stubs na solution gera atrito sem ganho:

- Os runtimes são heterogêneos (`net48`, `net8.0-windows`, `net10.0-windows`)
- Cada nova versão da RevitAPI exige reavaliar referências de stub
- A documentação de "como adicionar uma versão" sugere copiar V2026, o que
  agrava a duplicação que se quer eliminar
- A pasta `Revit.SharedSource` (ADR-0008) só faz sentido quando há duplicação
  real **entre adapters implementados**, não entre adapters stub

## Decisão
1. O escopo ativo do EVT-BIM passa a ser apenas **Revit 2025 e Revit 2026**.
2. Os projetos `DarivaBIM.Plugin.V2023/V2024/V2027` e
   `DarivaBIM.Revit.Adapters.V2023/V2024/V2027` (e seus testes) são removidos
   do repositório. O histórico permanece em Git para referência.
3. `Directory.Build.props` e `Directory.Build.targets` mantêm apenas as
   condições para `RevitVersion` 2025 e 2026.
4. O `RevitTargetFramework` ativo é `net8.0-windows` para ambas as versões.
5. Reabertura de suporte a outras versões (2024, 2027, 2028, etc.) exige
   um novo ADR e uma base de código já consolidada via `Plugin.SharedSource`
   (ver ADR-0016 quando existir).

## Implicações para arquitetura
- `DarivaBIM.Revit.Hosting` continua como single-target `net8.0-windows`,
  mas a fixação em `<RevitVersion>2026</RevitVersion>` precisa ser revisitada
  porque V2025 também consome Hosting. A decisão concreta (multitarget,
  source-shared, ou split) é tratada em fase posterior do refactor.
- `Plugin.SharedSource` (ADR-0011-bis a ser escrito) passa a concentrar o
  código antes duplicado entre V2025 e V2026.
  - **Atualizacao**: a centralizacao foi implementada em duas frentes,
    `Plugin.SharedSource` (ja existente) e `Revit.Adapters.SharedSource`
    (ADR-0017). O "ADR-0011-bis" referido aqui foi efetivamente
    coberto pelo ADR-0017.
- O ADR-0002 fica marcado como **Superseded by ADR-0015** e mantido como
  registro histórico da intenção original multi-versão ampla.

## Consequências
- Build, IDE e CI ficam mais leves
- `net48` e `net10.0-windows` saem do horizonte imediato
- Documentação de "adicionar nova versão" precisa ser reescrita após
  estabilização de SharedSource
- Reabrir 2027 (ou versão futura) é trivial uma vez que SharedSource cubra
  a maior parte do código não-versionado

## Reversibilidade
A remoção dos stubs é simples de reverter: cada projeto removido tinha
apenas `.csproj` + `Placeholder.cs`. Reativar uma versão futura significa
recriar esses dois arquivos e adicionar o projeto à solution.
