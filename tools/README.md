# tools/

Scripts auxiliares pra manutenção do projeto. **Não buildados**, não fazem parte do .csproj — rodados manualmente quando necessário.

## parse_tigre_payload.py

Converte o payload bruto Tigre (TXT no formato `## LINHA N — <nome>` + TSV) para `src/Core/DarivaBIM.Domain/Tigre/tigre_codes.json` no schema do Slice 2A.

### Quando re-rodar

- Tigre liberou SKUs novos (qualquer linha): atualize o `.txt` correspondente, re-rode.
- Mudança no schema do JSON: ajuste o `parse_payload`/`extract_dims` e re-rode.
- Quer regenerar o catálogo do zero: re-rode com o payload canônico.

### Uso

```
py tools/parse_tigre_payload.py <payload.txt> <output.json>
```

Exemplo (Slice 2A, payload canônico em `%TEMP%`):

```
py tools/parse_tigre_payload.py \
  "C:/Users/<você>/AppData/Local/Temp/tigre_skus_payload_2026-05-26.txt" \
  src/Core/DarivaBIM.Domain/Tigre/tigre_codes.json
```

### Determinismo

Saída é ordenada por `(productLine, code, description)` pra que diffs em PRs sejam legíveis. Indent 2, UTF-8 sem BOM, trailing newline.

### Validação

`DarivaBIM.Core.Tests/Domain/Tigre/TigreCatalogJsonValidationTests.cs` carrega o JSON gerado e roda as 6 validações do Slice 2A (productLine ∈ enum, kind ∈ enum, code/description não-vazios, código duplicado por productLine, diâmetros > 0). Se o script gerar entrada inválida, o test quebra.
