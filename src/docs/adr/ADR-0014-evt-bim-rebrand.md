# ADR-0014 — Rebrand parcial para EVT-BIM (identidade externa Tigre)

- Status: Aceito
- Data: 2026-04-29

## Contexto

Este repositório nasceu como base do plugin que será entregue à Tigre,
no escopo do programa **Engenharia de Valor Tigre — EVT-BIM**. A mesma
base arquitetural servirá, no futuro, à versão 2 do produto comercial
**DarivaBIM** (DarivaBIM V2). Ou seja: o repositório tem dois destinos
distintos, e os entregáveis terão identidades externas diferentes
(EVT-BIM para a Tigre, DarivaBIM para o mercado), mas a arquitetura
interna é compartilhada.

A arquitetura herdada das ADRs
[0001](ADR-0001-clean-architecture-core-agnostic.md),
[0002](ADR-0002-revit-multiversion-strategy.md),
[0003](ADR-0003-plugin-and-adapter-separation.md) e
[0010](ADR-0010-plugin-thin-composition-presentation.md) — Clean
Architecture com Core agnóstico à RevitAPI, multi-versão de Revit via
adapters, plugin fino + presentation desacoplada — é justamente o que
permite que a mesma base sirva a ambos os produtos sem refactor
substancial. O que muda entre eles é apenas a "casca" externa: aba do
Revit, painel da ribbon, `.addin`, instalador, pasta de instalação,
mensagens visíveis ao usuário.

Por isso, este ADR documenta um rebrand **parcial**: identidade externa
vira EVT-BIM; arquitetura, projetos C#, namespaces e assemblies
permanecem `DarivaBIM.*`.

## Decisão

Rebrand parcial, com a seguinte fronteira:

**Permanece `DarivaBIM.*` (estrutura interna):**

- Solution `DarivaBIM.sln`.
- Projetos C# (`.csproj`), nomes de assembly e root namespaces — todos
  os `DarivaBIM.Domain`, `DarivaBIM.Application`,
  `DarivaBIM.Infrastructure.*`, `DarivaBIM.Revit.*`,
  `DarivaBIM.Presentation.Wpf`, `DarivaBIM.Plugin.V2026` etc.
- O assembly principal continua `DarivaBIM.Plugin.V2026.dll`.
- Classes `DarivaBimRibbonDefinition` e `TigrePanelDefinition` mantêm os
  nomes (impacto técnico de renomear é desproporcional ao ganho de
  cosmética).
- A feature `TigreCodes` e o parâmetro contratual `Tigre: Código`
  permanecem com esses nomes — são específicos do domínio Tigre, e não
  bate com a marca EVT-BIM (Tigre é a empresa cliente; EVT-BIM é o nome
  do entregável).

**Vira EVT-BIM (identidade externa):**

- Aba do Revit (`DarivaBimRibbonDefinition.TabName`) → `"EVT-BIM"`.
- Nome do painel da ribbon (`TigrePanelDefinition.Name`) → `"EVT-BIM"`.
- Manifest `.addin`: arquivo renomeado para
  `EVT-BIM.V2026.addin`; `<Name>`, `<VendorId>` e `<VendorDescription>`
  atualizados; **novo `<AddInId>` GUID gerado** (não reuso do anterior,
  para permitir que EVT-BIM e o futuro DarivaBIM Classic/V2 coexistam na
  mesma máquina).
- Caminho de instalação no Revit: pasta
  `%ProgramData%\Autodesk\Revit\Addins\2026\EVT-BIM\` (antes:
  `...\DarivaBIM\`).
- Script de deploy (`deploy_revit_2026.cmd`): variáveis `ADDIN_NAME` e
  `ADDIN_FILE` apontam para EVT-BIM; bloco de limpeza inclui artefatos
  legados do nome antigo (`DarivaBIM` em ProgramData) para usuários que
  já tinham a versão anterior instalada.
- Títulos de `TaskDialog` visíveis ao usuário e nome de `Transaction` no
  ParameterEditor: `"TigreBIM"`/`"DarivaBIM"`/`"TigreBIM — X"` →
  `"EVT-BIM"`/`"EVT-BIM — X"`.
- Strings retornadas por `IExternalEventHandler.GetName()`:
  `"TigreBIM.*Handler"` → `"EvtBim.*Handler"` (afeta logs/telemetria).
- Caminho de persistência local do PipeCadMapper:
  `%APPDATA%\TigreBIM\pipecadmapper.json` →
  `%APPDATA%\EVT-BIM\pipecadmapper.json`. Migração silenciosa: se o
  arquivo legado existir e o novo não, o `Load` lê do legado e o
  próximo `Save` regrava no novo caminho.
- README na raiz do repositório descrevendo o produto como EVT-BIM.

**Permanece como está (descreve feature, não marca):**

- Cache de famílias em `%CommonApplicationData%\FamiliesImporterHub\Cache`
  — invalidaria o cache atual sem ganho.
- `User-Agent: FamiliesImporterHub` em `ApiClient` e
  `FamilyDownloadService` — descreve a feature, não o produto.
- Handler `FamiliesImporterHub.ImportFamilyHandler.GetName()`.
- Título da DockablePane "Importar Famílias".
- ADRs 0001..0013 (são histórico — a arquitetura descrita continua
  válida; o que muda é apenas a casca externa, documentada aqui).

## Consequências

- Os assemblies, projetos e namespaces continuam `DarivaBIM.*`. Quando o
  DarivaBIM V2 nascer dessa base, o caminho será trivial: trocar a casca
  externa de "EVT-BIM" para "DarivaBIM V2" (aba/painel/.addin/instalador
  /pasta de instalação) — provavelmente um find-replace mecânico nos
  mesmos pontos enumerados acima. Isso será objeto do **ADR-0015**.
- O novo `<AddInId>` permite coexistência: um mesmo Revit pode rodar
  EVT-BIM (entregue à Tigre) e o DarivaBIM Classic legado lado a lado
  sem conflito de identidade. A pasta de instalação separada
  (`...\Addins\2026\EVT-BIM\` vs `...\Addins\2026\DarivaBIM\`) reforça
  isso no nível do filesystem.
- O usuário final vê uma única identidade coerente: aba "EVT-BIM",
  painel "EVT-BIM", manifest `EVT-BIM.V2026.addin`, vendor
  `EVT-BIM`, mensagens com prefixo `"EVT-BIM —"`.
- Logs/telemetria antigas filtrando por `TigreBIM.*Handler` precisam ser
  ajustadas para `EvtBim.*Handler`.
- Usuários que usavam a versão anterior (TigreBIM, instalada como pasta
  `DarivaBIM`) terão o `.addin` legado removido pelo bloco de limpeza
  do deploy script. Preferências do PipeCadMapper são preservadas via
  migração silenciosa do arquivo `%APPDATA%\TigreBIM\pipecadmapper.json`.

## Pendências

- **ADR-0015 — DarivaBIM V2 a partir desta base.** Quando o DarivaBIM V2
  nascer dessa base, fazer o equivalente deste rebrand no sentido
  inverso: trocar a casca externa de "EVT-BIM" para "DarivaBIM V2"
  (aba, painel, `.addin`, instalador, pasta de instalação, mensagens
  ao usuário, vendor). Pode soar trivial, mas o ponto do ADR-0015 é
  documentar o que muda *e o que continua compartilhado* entre EVT-BIM
  (entregável Tigre) e DarivaBIM V2 (produto comercial), tornando
  explícita a coexistência das duas identidades sobre a mesma base.
