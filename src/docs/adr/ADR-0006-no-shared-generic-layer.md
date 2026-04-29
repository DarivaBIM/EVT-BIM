# ADR-0006 — Não criar uma camada `Common` / `Shared` / `Utils` genérica

## Status
Aceito.

## Contexto
Em projetos grandes é comum aparecer `Common`, `Shared`, `Utils`, `Helpers`,
`Manager`, `Funcoes`, `Diversos` e `Outros`. Esses nomes são entropia: tudo
acaba ali e ninguém sabe o que cada coisa faz. Em poucas semanas viram um
acoplamento global com regras de negócio entranhadas.

## Decisão
**Proibir** os seguintes nomes em projetos, namespaces, pastas e classes:
`Utils`, `Helpers`, `Manager`, `Shared`, `Diversos`, `Outros`, `Funcoes`,
`Complementos`, `Common` (este último a menos que seja literalmente um pacote
estável de tipos primitivos cross-camada — não é o nosso caso).

Quando aparecer a tentação de "preciso de um lugar para essa função utilitária
nova", a resposta é:
- Se é regra de negócio → `Domain`.
- Se orquestra → `Application` (UseCase ou Service).
- Se é tradução para o Revit → `Adapter` apropriado.
- Se é I/O concreto → `Infrastructure.{Api|Persistence|Licensing|Telemetry}`.

## Nomes preferidos por responsabilidade
`UseCases`, `Contracts`, `DTOs`, `Adapters`, `Repositories`, `Readers`,
`Writers`, `Mapping`, `Factories`, `Providers`, `Validators`, `Filters`,
`Transactions`, `Definitions`, `Registries`, `Hosting`, `Resources`.

## Consequências
- Pastas com nomes ricos em significado.
- Nenhuma classe vira "lixeira de função utilitária".
- Pequena duplicação localizada é preferível a uma abstração global ruim.
