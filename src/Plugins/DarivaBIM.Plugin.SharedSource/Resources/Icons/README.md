# Ribbon Icons — DarivaBIM.Plugin.V2026

Esta pasta concentra os ícones usados pela ribbon do plugin para Revit 2026.

Os caminhos relativos esperados pelos `RibbonButtonDefinition` (em
`Ribbon/Buttons/*.cs`) seguem o padrão:

```
Ribbon/Resources/Icons/<nome>_16.png   # ícone pequeno
Ribbon/Resources/Icons/<nome>_32.png   # ícone grande
```

## Recomendações de tamanho

- Ícones grandes (`*_32.png`): preferencialmente **32x32 px**.
- Ícones pequenos (`*_16.png`): preferencialmente **16x16 px**.
- Formato: PNG com canal alfa (transparência).

## Nomes esperados

Cada arquivo abaixo é referenciado por um botão da ribbon. Enquanto não
existirem fisicamente, os botões serão criados sem ícone e o caminho
permanece reservado para quando a arte for adicionada.

| Botão                    | Ícone grande                       | Ícone pequeno                      |
| ------------------------ | ---------------------------------- | ---------------------------------- |
| Families Importer Hub    | `families_importer_hub_32.png`     | `families_importer_hub_16.png`     |
| PipeCADMapper            | `pipe_cad_mapper_32.png`           | `pipe_cad_mapper_16.png`           |
| Codificar Tubos          | `pipe_codes_32.png`                | `pipe_codes_16.png`                |
| Adicionar Prolongadores  | `floor_drain_extension_32.png`     | `floor_drain_extension_16.png`     |
| Parâmetros em Lote       | `batch_parameter_editor_32.png`    | `batch_parameter_editor_16.png`    |

## Cópia para a saída do build

O Shared Project `DarivaBIM.Plugin.SharedSource.projitems` já inclui um
`ItemGroup` com `Resources\Icons\*.png` configurado como
`Link="Ribbon\Resources\Icons\%(Filename)%(Extension)"` e
`CopyToOutputDirectory = PreserveNewest`. Basta soltar os PNGs nesta pasta
— os plugins V2025 e V2026 herdam automaticamente.
