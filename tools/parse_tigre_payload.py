#!/usr/bin/env python3
"""Parser do payload Tigre (.txt) para tigre_codes.json.

Entrada: arquivo .txt com seções `## LINHA N — <nome>` seguidas de linhas
TSV `item<TAB>code<TAB>description` (uma por SKU).

Saída: JSON pretty-printed (indent 2, UTF-8 sem BOM) com objetos do schema:
    {
        "description": str,
        "code": int,
        "diameterMm": int,            // 0 quando ausente
        "productLine": str,
        "kind": str,
        "dn1": int | null,
        "dn2": int | null,
        "pn": str | null              // "12.5", "20", "25" (Slice 2B)
    }

Uso:
    py tools/parse_tigre_payload.py <payload.txt> <output.json>

Notas de parsing:
- Entries TIGREFire usam polegada como unidade nativa. O parser captura
  números na descrição com regex genérico e depois converte via
  INCH_TO_MM quando productLine == "TIGREFire" (Slice 2B).
- PN (classe de pressão PPR — 12.5, 20, 25) é extraída por regex separado
  e armazenada como string pra preservar precisão decimal.

Regenere o JSON sempre que a Tigre liberar SKUs novos: atualize o .txt
(mesmo formato) e re-rode o script — o output é determinístico (ordenado
por productLine + code + description) pra diffs legíveis em PRs.
"""
import json
import re
import sys
from collections import Counter

LINE_LABELS = {
    "Esgoto Série Reforçada (SR)": "SR",
    "Esgoto Série Normal (SN)": "SN",
    "REDUX": "REDUX",
    "Soldável (PVC água fria)": "Soldável",
    "Registros + Válvulas + Caixas": "Registros",
    "ClicPEX": "ClicPEX",
    "AQUATHERM (CPVC água quente)": "AQUATHERM",
    "TIGREFire (CPVC incêndio)": "TIGREFire",
    "PPR (água quente PPR)": "PPR",
}

INCH_TO_MM = {
    "1/2": 13, "3/4": 19, "1": 25, "1.1/4": 32, "1.1/2": 38,
    "2": 51, "2.1/2": 64, "3": 76, "4": 102, "5": 127, "6": 152,
}


def parse_inch(s: str):
    return INCH_TO_MM.get(s.strip())


def classify_kind(desc: str) -> str:
    d = desc.lower()
    if d.startswith("tubo "):
        return "pipe"
    if d.startswith("cap "):
        return "cap"
    if d.startswith("joelho ") or " joelho " in d:
        return "elbow"
    if d.startswith("tê ") or d.startswith("te ") or " tê " in d:
        return "tee"
    reducer_markers = (
        "bucha de redução", "bucha redução",
        "redução excêntrica", "luva de redução",
    )
    if any(m in d for m in reducer_markers):
        return "reducer"
    if any(d.startswith(p) for p in (
        "luva ", "união ", "conector ", "adaptador ", "conexão ",
        "curva ", "anel ", "cruzeta ", "junção ", "flange ", "manifold ",
    )):
        return "fitting"
    if any(d.startswith(p) for p in (
        "registro ", "válvula ", "torneira ",
    )):
        return "valve"
    if any(d.startswith(p) for p in (
        "caixa ", "ralo ", "corpo ", "prolongamento ",
        "suporte ", "terminal ", "abraçadeira ",
    )):
        return "accessory"
    return "fitting"


# 3 dimensões: "25x20x25mm", "DN75x75x75", "100X75X50"
RE_3DIMS = re.compile(
    r'(?<![\d.,/])(?:DN\s*)?(\d+)\s*[xX]\s*(\d+)\s*[xX]\s*(\d+)(?!\s*[\'\"])',
    re.IGNORECASE,
)
# Par mm explícito "25mmx20mm" — sem `\b` problemático entre "m" e "x"
RE_PAIR_MMXMM = re.compile(
    r'(?<![\d.,/])(\d+)\s*mm\s*[xX]\s*(\d+)\s*mm\b',
    re.IGNORECASE,
)
# Par com polegada no 2º termo: "20x1/2"", "3x2.1/2'", "TIGREFire1x1/2'".
# 1º termo aceita fracionária pra cobrir casos tipo "1.5x..." (não no
# payload atual mas defensivo).
RE_PAIR_INCH = re.compile(
    r'(?<![\d.,/])(\d+(?:\.\d+)?)\s*[xX]\s*(\d+(?:\.\d+)?(?:/\d+)?)\s*[\'\"]',
)
# Par polegada-polegada: "1'x3/4'", "1.1/4'x1'", "3/4x1/2'" — dn1 polegada.
# Aceita polegada inteira no 1º grupo (exige aspa/apóstrofe entre os dois
# números, OU `/` obrigatório no 1º grupo se não houver aspa antes do x).
RE_PAIR_INCH_INCH = re.compile(
    r'(?<![\d.,/])(\d+(?:\.\d+)?(?:/\d+)?)[\'\"]\s*[xX]\s*'
    r'(\d+(?:\.\d+)?(?:/\d+)?)\s*[\'\"]'
)
# Par polegada-polegada SEM aspas no primeiro: "3/4x1/2'" — 1º termo
# precisa ser fração pra evitar match contra "20x1/2'" (mm-polegada).
RE_PAIR_FRACINCH = re.compile(
    r'(?<![\d.,/])(\d+(?:\.\d+)?/\d+)\s*[xX]\s*'
    r'(\d+(?:\.\d+)?(?:/\d+)?)\s*[\'\"]'
)
# Par numérico (mm/DN): "DN150x150", "75X50", "25x20mm"
RE_PAIR_MM = re.compile(
    r'(?<![\d.,/])(?:DN\s*)?(\d+)\s*[xX]\s*(\d+)(?![\'\"\d.,/])',
    re.IGNORECASE,
)
# DN sozinho: "DN40"
RE_DN_SOLO = re.compile(r'(?<![\d.,/])DN\s*(\d+)\b', re.IGNORECASE)
# mm sozinho: "20mm", "DN 15mm", "16mm - 100m"
RE_MM_SOLO = re.compile(r'(?<![\d.,/])(\d+)\s*mm\b')
# Polegada com fração: "1.1/4'", "1/2""
RE_INCH_FRAC = re.compile(r'(?<![\d.,/])(\d+(?:\.\d+)?/\d+)\s*[\'\"]')
# Polegada inteira: "1'", "2""
RE_INCH_INT = re.compile(r'(?<![\d.,/])(\d+)\s*[\'\"]')
# Extração de PN (classe de pressão PPR): "PN 20", "PN 12.5", "PN25"
RE_PN = re.compile(r'\bPN\s*(\d+(?:\.\d+)?)\b', re.IGNORECASE)


def _maybe_inch_to_mm(n, product_line: str | None):
    """Em TIGREFire, números pequenos (<19mm = 3/4") provavelmente são
    polegada inteira por parse cru. Tenta INCH_TO_MM(str(n)) e devolve
    o equivalente em mm quando casa. Senão devolve n inalterado."""
    if n is None or product_line != "TIGREFire":
        return n
    if n >= 19:
        return n
    mm = INCH_TO_MM.get(str(n))
    return mm if mm is not None else n


def extract_dims(desc: str, product_line: str | None = None):
    d = desc
    raw = _extract_dims_raw(d)

    # Pós-processamento TIGREFire: a linha usa polegada nativa, então
    # números pequenos capturados como int cru ("Luva de Transição
    # TIGREFire 1x1" → bruto (1, 1, 1)) precisam ser interpretados como
    # polegada e convertidos pra mm.
    diam, dn1, dn2 = raw
    if product_line == "TIGREFire":
        diam = _maybe_inch_to_mm(diam, product_line)
        dn1 = _maybe_inch_to_mm(dn1, product_line)
        dn2 = _maybe_inch_to_mm(dn2, product_line)

        # Fallback: descrição TIGREFire termina com polegada SEM aspa
        # ("Conector Macho TIGREFire 1.1/2"). Tenta último token como
        # polegada se ainda não achamos diâmetro.
        if diam is None:
            tokens = d.rstrip().split()
            if tokens:
                last = tokens[-1].strip("'\"´")
                mm = INCH_TO_MM.get(last)
                if mm is not None:
                    diam = dn1 = mm

    return (diam, dn1, dn2)


def _extract_dims_raw(d: str):
    """Captura bruta de números na descrição. Conversão polegada→mm é
    feita em extract_dims pós-processamento (depende de productLine)."""
    m = RE_PAIR_INCH_INCH.search(d)
    if m:
        mm1 = parse_inch(m.group(1))
        if mm1:
            return (mm1, mm1, None)
    m = RE_PAIR_FRACINCH.search(d)
    if m:
        mm1 = parse_inch(m.group(1))
        if mm1:
            return (mm1, mm1, None)
    m = RE_3DIMS.search(d)
    if m:
        a, b, _c = int(m.group(1)), int(m.group(2)), int(m.group(3))
        return (a, a, b)
    m = RE_PAIR_MMXMM.search(d)
    if m:
        a, b = int(m.group(1)), int(m.group(2))
        return (a, a, b)
    m = RE_PAIR_INCH.search(d)
    if m:
        # 1º termo é float-ish (ex "1.5") mas o payload Tigre só tem
        # inteiros aqui; int() do float-str falha, então tenta cast int
        # direto primeiro.
        s1 = m.group(1)
        a = int(s1) if "." not in s1 else int(float(s1))
        # 2º termo é polegada fracionária — não usado como dn2 (rosca
        # ignorada), pra preservar semântica histórica.
        return (a, a, None)
    m = RE_PAIR_MM.search(d)
    if m:
        a, b = int(m.group(1)), int(m.group(2))
        return (a, a, b)
    m = RE_DN_SOLO.search(d)
    if m:
        a = int(m.group(1))
        return (a, a, None)
    m = RE_MM_SOLO.search(d)
    if m:
        a = int(m.group(1))
        return (a, a, None)
    m = RE_INCH_FRAC.search(d)
    if m:
        mm = parse_inch(m.group(1))
        if mm:
            return (mm, mm, None)
    m = RE_INCH_INT.search(d)
    if m:
        mm = parse_inch(m.group(1))
        if mm:
            return (mm, mm, None)
    return (None, None, None)


def extract_pn(desc: str):
    """Extrai a classe de pressão PN da descrição (PPR). Retorna string
    pra preservar precisão decimal ('12.5' vs '12' arredondado)."""
    m = RE_PN.search(desc)
    if not m:
        return None
    return m.group(1)


def parse_payload(payload_path: str):
    entries = []
    current = None
    with open(payload_path, encoding='utf-8') as f:
        for raw in f:
            line = raw.rstrip('\n')
            if line.startswith("## LINHA"):
                if "—" in line:
                    label = line.split("—", 1)[1].strip()
                    current = LINE_LABELS.get(label)
                    if current is None:
                        print(f"WARN: label desconhecido: {label!r}",
                              file=sys.stderr)
                continue
            stripped = line.strip()
            if not stripped or stripped.startswith("#"):
                continue
            if current is None:
                continue
            parts = line.split("\t")
            if len(parts) < 3:
                parts = re.split(r'\s{2,}|\t', line.strip(), maxsplit=2)
            if len(parts) < 3:
                print(f"WARN: linha ilegível: {line!r}", file=sys.stderr)
                continue
            _item = parts[0].strip()
            code_str = parts[1].strip()
            description = parts[2].strip()
            try:
                code = int(code_str)
            except ValueError:
                print(f"WARN: code não-inteiro: {code_str!r} em {line!r}",
                      file=sys.stderr)
                continue
            kind = classify_kind(description)
            diameter_mm, dn1, dn2 = extract_dims(description, current)
            pn = extract_pn(description)
            # TigreRawCatalogRow.DiameterMm é int (não nullable) por
            # compat com PipeCodes — emite 0 quando ausente; o filtro
            # `r.DiameterMm > 0` no ctor do TigreCatalog descarta essas
            # entradas. dn1/dn2 são int?, emitidos como null quando ausentes.
            entries.append({
                "description": description,
                "code": code,
                "diameterMm": diameter_mm if diameter_mm is not None else 0,
                "productLine": current,
                "kind": kind,
                "dn1": dn1,
                "dn2": dn2,
                "pn": pn,
            })
    return entries


def write_json(entries, output_path: str):
    entries_sorted = sorted(
        entries,
        key=lambda e: (e["productLine"], e["code"], e["description"]),
    )
    with open(output_path, 'w', encoding='utf-8', newline='\n') as f:
        json.dump(entries_sorted, f, indent=2, ensure_ascii=False)
        f.write("\n")
    return entries_sorted


def print_stats(entries):
    total = len(entries)
    by_line = Counter(e["productLine"] for e in entries)
    by_kind = Counter(e["kind"] for e in entries)
    null_diam = sum(1 for e in entries if not e["diameterMm"])
    with_pn = sum(1 for e in entries if e["pn"])
    print(f"Total entradas: {total}")
    print("Por productLine:")
    for line, count in sorted(by_line.items()):
        print(f"  {line:12s} {count}")
    print("Por kind:")
    for k, c in sorted(by_kind.items()):
        print(f"  {k:10s} {c}")
    pct = (100.0 * null_diam / total) if total else 0.0
    print(f"diameterMm=null: {null_diam} ({pct:.1f}%)")
    print(f"com pn: {with_pn}")

    # Validações Slice 2B: TIGREFire deve ter dm >= 19 em todos
    # (polegada minima = 3/4"=19mm); PPR deve ter ≥3 valores distintos
    # de PN em tubos.
    tf_low = [e for e in entries
              if e["productLine"] == "TIGREFire" and 0 < e["diameterMm"] < 19]
    if tf_low:
        print(f"ATENÇÃO: {len(tf_low)} entries TIGREFire com dm<19mm:")
        for e in tf_low:
            print(f"  code={e['code']:>10}  dm={e['diameterMm']}  "
                  f"{e['description']}")

    ppr_pns = sorted({
        e["pn"] for e in entries
        if e["productLine"] == "PPR" and e["pn"]
    })
    print(f"PPR PNs distintos: {ppr_pns}")


def main(argv):
    if len(argv) != 3:
        print("Uso: py parse_tigre_payload.py <payload.txt> <output.json>",
              file=sys.stderr)
        return 2
    payload_path, output_path = argv[1], argv[2]
    entries = parse_payload(payload_path)
    write_json(entries, output_path)
    print_stats(entries)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
