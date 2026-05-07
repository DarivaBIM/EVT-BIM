# Filter Icons — Families Importer

Ícones renderizados nos *chips* de filtro por sistema na aba Biblioteca
Tigre (`FamiliesPage.xaml` → `TagDotStyle`). Cada chip mostra um ícone
PNG sobre um fundo pastel; a cor de fundo, o nome legível e o ícone
fallback (caractere Segoe MDL2) ficam declarados em
`Plugins/DarivaBIM.Plugin.SharedSource/Ui/TagFilterOption.cs`.

## Onde colocar

Solte o PNG nesta pasta com o nome listado abaixo. O arquivo é copiado
para `Ribbon/Resources/FilterIcons/<arquivo>.png` no diretório de saída
de cada plugin (V2025/V2026) via `.projitems` (`<None CopyToOutputDirectory>`).

Não é necessário alterar `.csproj`. Basta rebuild do plugin para o
ícone aparecer no chip.

## Recomendações de arte

- Tamanho: **64x64 px** (decodificado para 80px no chip; cobre HiDPI até ~3x).
- Formato: PNG com canal alfa.
- Estilo: monocromático/glyph, traço leve, alinhado para casar visualmente
  com o tom da cor de fundo do chip.
- Cor recomendada do traço: a cor "sugerida" da tabela abaixo, usando o
  HEX da coluna correspondente — assim o ícone se integra ao chip mesmo
  quando o usuário aproxima a vista.

## Mapa de categorias → ícone → cor

| Categoria              | Ícone (descrição)             | Cor sugerida       | HEX        | Arquivo esperado              |
|------------------------|-------------------------------|--------------------|------------|-------------------------------|
| Ponto de utilização    | Vaso sanitário                | Cinza neutro       | `#616161`  | `ponto_de_utilizacao.png`     |
| Poço                   | Bomba manual / retirada       | Âmbar escuro       | `#C88719`  | `poco.png`                    |
| Irrigação              | Folha com gotas               | Verde-oliva        | `#6B8E23`  | `irrigacao.png`               |
| Válvula                | Registro                      | Verde-petróleo     | `#00796B`  | `valvula.png`                 |
| Tratamento de esgoto   | Fossa / tanque                | Marrom terroso     | `#6D4C41`  | `tratamento_de_esgoto.png`    |
| Bombas                 | Bomba centrífuga / motor      | Laranja técnico    | `#EF6C00`  | `bombas.png`                  |
| Água Fria              | Gota                          | Azul               | `#1565C0`  | `agua_fria.png`               |
| Água Quente            | Gota                          | Vermelho quente    | `#D84343`  | `agua_quente.png`             |
| Piscina                | Escada + ondas                | Azul-céu           | `#039BE5`  | `piscina.png`                 |
| Reservatório           | Caixa d'água                  | Índigo             | `#3949AB`  | `reservatorio.png`            |
| Combate a Incêndio     | Hidrante                      | Vermelho-escuro    | `#B71C1C`  | `combate_a_incendio.png`      |
| Esgoto                 | Sifão                         | Verde escuro       | `#2E7D32`  | `esgoto.png`                  |
| Pluvial                | Nuvem com chuva               | Roxo azulado       | `#5E60CE`  | `pluvial.png`                 |
| Caixas e Ralos         | Ralo (vista de topo)          | Cinza-ardósia      | `#546E7A`  | `caixas_e_ralos.png`          |

## Fallback

Quando o PNG não existir nesta pasta, o chip continua sendo renderizado
— só que mostrando o glyph Segoe MDL2 do mapa interno em
`TagFilterOption.ResolvePalette`. Não há erro no build nem no runtime.
