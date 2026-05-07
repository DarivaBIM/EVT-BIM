# Filter Icons — Families Importer

Ícones renderizados nos *chips* de filtro por sistema na aba Biblioteca
Tigre (`FamiliesPage.xaml` → `TagDotStyle`) e nos badges miniaturizados
no rodapé dos cards de família.

## Implementação

A partir desta iteração os ícones são **vetoriais**: cada arquivo `.svg`
desta pasta é a fonte de design, mas a renderização em runtime usa
`DrawingImage` congelados construídos diretamente em código no
`Ui/Models/SistemaIconLoader.cs`. Os SVGs originais permanecem aqui
apenas como referência (não são copiados para o output do build).

Vantagens vs. raster:

- Sem perda de nitidez em qualquer DPI (HiDPI, 4K) — o WPF rasteriza
  sob demanda no tamanho lógico do `Image` consumidor.
- Sem cache de bitmap por tamanho lógico → menos memória.
- Brushes pré-frozen, reusados entre todas as instâncias do mesmo
  sistema (uma única alocação de `Pen` + `SolidColorBrush` por sistema).

## Atualizando um ícone

1. Abra o SVG no Figma (ou outro editor) e altere o desenho.
2. Salve sobre o arquivo `.svg` aqui na pasta.
3. Abra `SistemaIconLoader.BuildCatalog` em
   `Ui/Models/SistemaIconLoader.cs` e atualize a entrada do sistema
   correspondente: copie o(s) `path d="…"` do novo SVG e cole
   substituindo a string anterior. Cores `<circle>`/`<ellipse>` viram
   `Ellipse(cx, cy, rx, ry)`.
4. Recompile o plugin — não há cópia de PNG, não há rebuild de
   recurso linkado.

## Convenções de arte

- viewBox **64×64** (mesma origem dos SVGs do Figma).
- Stroke colorido com a cor de marca do sistema; **sem fill**, só
  contorno. `stroke-width="3"`, `stroke-linecap="round"`,
  `stroke-linejoin="round"`.
- O `SistemaIconLoader.MakeStroked` aplica esses defaults globalmente
  — não tente codificar variações por ícone, prefira manter a coerência
  visual com os 14 já estabelecidos.
- Se um ícone novo precisar de fill ou múltiplas cores, estenda o
  helper antes de adicionar a entrada no catálogo (mantém o catálogo
  em si curto e legível).

## Mapa de categorias → ícone → cor

| Categoria              | Ícone (descrição)             | Cor                |
|------------------------|-------------------------------|--------------------|
| Água Fria              | Gota                          | `#1565C0`          |
| Água Quente            | Gota                          | `#D84343`          |
| Pluvial                | Nuvem com chuva               | `#5E60CE`          |
| Esgoto                 | Sifão                         | `#2E7D32`          |
| Combate a Incêndio     | Hidrante                      | `#B71C1C`          |
| Piscina                | Escada + ondas                | `#039BE5`          |
| Irrigação              | Folhas em vaso                | `#6B8E23`          |
| Reservatório           | Caixa d'água                  | `#0E7490`          |
| Bombas                 | Bomba centrífuga              | `#EF6C00`          |
| Válvula                | Registro                      | `#00796B`          |
| Caixas e Ralos         | Ralo (vista de topo)          | `#546E7A`          |
| Tratamento de Esgoto   | Fossa com azulejos            | `#6D4C41`          |
| Poço                   | Bomba manual + gota           | `#C88719`          |
| Ponto de Utilização    | Vaso sanitário                | `#616161`          |

## Fallback

Quando um nome de sistema não existe no catálogo do `SistemaIconLoader`,
`Load` retorna `null` e o chip cai no glyph Segoe MDL2 declarado em
`TagFilterOption.ResolvePalette`. Não há erro no build nem no runtime.
