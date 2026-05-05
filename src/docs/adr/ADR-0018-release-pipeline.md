# ADR-0018 — Release pipeline: script local agora, CI hospedado depois

## Status
Aceito. Marca o inicio do fluxo de distribuicao publica do EVT-BIM e
estabelece em duas fases.

## Contexto

O ADR-0015 fixou Revit 2025 e 2026 como versoes ativas. O commit `8ed563e`
adicionou o pipeline de empacotamento Inno Setup (`build_installer.cmd` +
`EVT-BIM.iss`), gerando um `.exe` instalador unico. Falta o passo seguinte:
**como esse .exe sai da maquina dev e chega no usuario final**.

Restricao chave: o build referencia `RevitAPI.dll` e `RevitAPIUI.dll`
diretamente da instalacao local do Revit
(`src/Build/Directory.Build.targets:31-38`, `HintPath` para
`C:\Program Files\Autodesk\Revit 20XX\`). Isso significa que **runners
hospedados pelo GitHub nao conseguem buildar o projeto**, porque nao tem
Revit instalado.

Tres opcoes foram consideradas:

| Opcao | Como funciona | Custo de mudanca |
|---|---|---|
| (A) Self-hosted runner | Runner GH Actions instalado na maquina dev (com Revit). | Baixo — zero mudanca no build, ~15 min de setup. |
| (B) NuGet `Nice3point.Revit.Api.*` | Trocar `<Reference HintPath>` por `<PackageReference>` para os pacotes Nice3point. Runner hospedado consegue buildar. | Medio — toca `Directory.Build.targets`, requer revalidar os dois plugins. |
| (C) Script local `release.ps1` | Tagga, builda, publica no GitHub Releases via `gh`. Sem CI. | Minimo — script novo, zero mudanca no build. |

## Decisao

**Fase 1 (agora): Opcao C — script local `release.ps1`.**

Justificativa: o projeto tem **um unico desenvolvedor** (Matheus) e esta
saindo do uso interno para distribuicao publica beta. Lancamentos serao
raros (a cada feature/fix significativo) e sempre disparados por ele. CI
de verdade adiciona complexidade que nao paga seus custos nessa fase.

`release.ps1` (em `src/Build/Installers/`) faz, em ordem:

1. Valida `gh` autenticado, `dotnet` no PATH, branch, working tree limpo,
   tag inexistente local e remoto.
2. Bump de versao em **dois lugares simultaneamente** —
   `src/Build/Directory.Build.props` (`<Version>`, `<AssemblyVersion>`,
   `<FileVersion>`) e `src/Build/Installers/EVT-BIM.iss` (`#define
   AppVersion`). Resolve a duplicacao apontada no `README.md` do
   pipeline de installer.
3. Roda `build_installer.cmd` e exige que o `.exe` esperado exista.
4. So entao commita, tagga, faz push e cria o release com `gh release
   create --generate-notes` (notas auto-geradas a partir dos commits).

Ordem intencional: **a tag so existe se o build passou**. Falha de build
deixa os arquivos com bump nao-commitado, recuperavel com `git restore`.

**Fase 2 (futuro): Opcao B — NuGet Nice3point + GH Actions hospedado.**

Quando migrar e por que B em vez de A:

- **A (self-hosted runner)** mantem a maquina dev como ponto unico de
  falha, so que com mais maquinaria. Nao resolve nenhuma das pressoes
  abaixo. Descartado.
- **B (Nice3point)** e o padrao da comunidade Revit dev (RevitLookup,
  pyRevit, etc.). Builds passam a ser reproduziveis em qualquer runner,
  PRs ganham CI gratuito, releases podem partir de qualquer commit por
  qualquer dev.

Mudanca concreta em B (nao implementada agora — registrada para a Fase 2):

```xml
<!-- src/Build/Directory.Build.targets -->
<ItemGroup Condition="'$(RevitVersion)' != ''">
  <PackageReference Include="Nice3point.Revit.Api.Core" Version="$(RevitVersion).0.0">
    <PrivateAssets>all</PrivateAssets>
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
  <PackageReference Include="Nice3point.Revit.Api.UI"   Version="$(RevitVersion).0.0">
    <PrivateAssets>all</PrivateAssets>
    <ExcludeAssets>runtime</ExcludeAssets>
  </PackageReference>
</ItemGroup>
```

`ExcludeAssets=runtime` preserva o efeito de `<Private>false</Private>`
atual: as DLLs do RevitAPI nao vao para o `bin/` do plugin (o Revit ja
carrega as suas).

## Gatilhos para mover para Fase 2

Migrar quando **qualquer um** acontecer:

1. **Segundo desenvolvedor** entrar no projeto (release deixa de ser de
   uma maquina so).
2. **Frequencia de release** subir para mais de uma por mes de forma
   sustentada (CI economiza tempo manual real).
3. **Hotfix com SLA < 24h** virar requisito (depender da maquina X
   estar ligada e proibitivo).
4. **PRs externos** comecarem a chegar (precisam de CI de validacao
   automatica).
5. **Migrar para uma terceira versao do Revit** (2027 etc.) — bom
   momento para pagar a divida porque o build ja vai ser tocado.

Ate la, mover seria over-engineering.

## Itens fora do escopo deste ADR

Lancamento publico tambem precisa de tres coisas que **nao** estao
ligadas a escolha de pipeline e devem ser decididas separadamente:

- **Code Signing** (~US$ 100-400/ano). Sem isso, SmartScreen mostra
  "Editor desconhecido" no primeiro download — afasta usuarios
  nao-tecnicos. Maior atrito real para publico geral; maior que CI.
- **Canal de distribuicao secundario**: Autodesk App Store alcanca
  arquitetos/engenheiros nao-tecnicos que nao baixariam de GitHub.
  Tem revisao e regras de UX.
- **Licenca explicita** no repo (`LICENSE`). Define se Tigre quer
  manter codigo fechado distribuindo so o `.exe`, ou abrir
  (MIT/Apache).

Esses tres itens sao bloqueantes para "publico geral de verdade" mas
nao para a Fase 1 (beta com clientes selecionados).

## Consequencias positivas

- `release.ps1` resolve a duplicacao de versao (props + .iss) num unico
  comando — bug latente do pipeline de installer eliminado.
- Zero infraestrutura nova: nada para manter, nada para pagar.
- Releases ganham tag git versionada e binario anexado, dois pre-requisitos
  para qualquer migracao futura para CI.
- Notas de release auto-geradas via `gh --generate-notes` dao um
  changelog publico minimo de graca.

## Consequencias negativas

- Lancar release exige a maquina dev ligada com Revit instalado e o
  Inno Setup. Aceitavel enquanto Matheus for o unico releaser.
- Build nao e reproduzivel por terceiros — quem clonar o repo e tentar
  rodar `build_installer.cmd` precisa de Revit local. PRs externos nao
  conseguem ser validados sem isso.
- Sem Code Signing, instalador mostra warning de SmartScreen. Aceitavel
  para beta interno/clientes-piloto, **nao** para publico geral.

## Reversibilidade

Trivial. Fase 1 adiciona apenas um arquivo (`release.ps1`) e este ADR.
Apagar os dois reverte a decisao sem efeito colateral. Versao continua
funcionando manualmente via `build_installer.cmd` + `gh release create`.

## Relacao com outros ADRs

- **Depende de ADR-0015**: o conjunto de versoes ativas (2025/2026) e
  o que o `EVT-BIM.iss` cobre como componentes opcionais.
- **Sucede o commit `8ed563e`** que introduziu o pipeline de empacotamento;
  este ADR adiciona a camada de distribuicao em cima dele.
- **Nao afeta** a arquitetura interna (ADR-0001 a ADR-0017). O pipeline de
  release ve o codigo como caixa-preta produzida por `dotnet publish`.
