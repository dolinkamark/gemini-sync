#!/usr/bin/env python3
import json
import re

# ========= CONFIG (edit these) =========
INPUT_FILE = r"E:\Temp\Ymir\utility_connections.txt"
OUTPUT_FILE = r"E:\Temp\Ymir\utility_connections.json"          # e.g. r"output.json" (None => print to stdout)
OUTPUT_AS_ARRAY = True     # True => always output a JSON array
SKIP_HEADERS = False         # True => skip SSMS header + dashed separator lines
# ======================================

COLUMNS = [
    "CustomerId",
    "ExternalAgreementId",
    "AgreementId",
    "AgreementType",
    "PlaceNr",
    "PlaceTypeId",
    "PlaceType",
    "FromDate",
    "ToDate",
    "CreatedAt",
    "UpdatedAt",
]

INT_FIELDS = {
    "CustomerId",
    "PlaceNr",
    "PlaceTypeId",
    "AgreementId",
    "ExternalAgreementId",
}
BOOL_FIELDS = {"HasLock"}
DATETIME_FIELDS = {"FromDate", "ToDate", "CreatedAt", "UpdatedAt"}

_SQL_DT_RE = re.compile(
    r"^(?P<date>\d{4}-\d{2}-\d{2})\s(?P<time>\d{2}:\d{2}:\d{2})\.(?P<ms>\d{1,7})$"
)

def sql_datetime_to_iso(s: str) -> str:
    s = s.strip()
    m = _SQL_DT_RE.match(s)
    if not m:
        return s.replace(" ", "T", 1)

    ms = m.group("ms")
    ms3 = (ms + "000")[:3]  # keep milliseconds (3 digits)
    return f"{m.group('date')}T{m.group('time')}.{ms3}"

def split_row(line: str) -> list[str]:
    # Prefer tabs (SSMS "Results to Text"), fallback to 2+ spaces
    parts = [p for p in line.strip().split("\t") if p != ""]
    if len(parts) <= 1:
        parts = [p for p in re.split(r"\s{2,}", line.strip()) if p != ""]
    return parts

def parse_row_to_json(line: str) -> dict:
    parts = split_row(line)

    if len(parts) != len(COLUMNS):
        raise ValueError(
            f"Expected {len(COLUMNS)} fields but got {len(parts)}.\n"
            f"Line: {line}\nParsed fields: {parts}"
        )

    obj = {}
    for col, raw in zip(COLUMNS, parts):
        raw = raw.strip()

        # Handle NULL values - check before any type-specific parsing
        if not raw or raw.upper() == "NULL" or raw.upper() == "(NULL)":
            obj[col] = None
            continue

        if col in INT_FIELDS:
            obj[col] = int(raw)
        elif col in BOOL_FIELDS:
            if raw.lower() in {"1", "true", "yes", "y"}:
                obj[col] = True
            elif raw.lower() in {"0", "false", "no", "n"}:
                obj[col] = False
            else:
                raise ValueError(f"Cannot parse boolean for {col}: {raw}")
        elif col in DATETIME_FIELDS:
            obj[col] = sql_datetime_to_iso(raw)
        else:
            obj[col] = raw

    return obj

def should_skip_line(line: str, skip_headers: bool) -> bool:
    if not line.strip():
        return True
    if not skip_headers:
        return False

    s = line.strip()
    # Skip SSMS header line (contains column names)
    if "CustomerId" in s and "AgreementLineId" in s:
        return True
    # Skip dashed separator line like "-----  -----  -----"
    if re.fullmatch(r"[-\s]+", s) and "-" in s:
        return True

    return False

def main():
    results = []
    with open(INPUT_FILE, "r", encoding="utf-8") as f:
        for lineno, line in enumerate(f, start=1):
            if should_skip_line(line, SKIP_HEADERS):
                continue
            try:
                results.append(parse_row_to_json(line))
            except Exception as e:
                raise RuntimeError(f"Failed parsing line {lineno}: {line.rstrip()}") from e

    output_obj = results if (OUTPUT_AS_ARRAY or len(results) != 1) else results[0]
    output_json = json.dumps(output_obj, indent=2, ensure_ascii=False)

    if OUTPUT_FILE:
        with open(OUTPUT_FILE, "w", encoding="utf-8") as out:
            out.write(output_json)
            out.write("\n")
    else:
        print(output_json)

if __name__ == "__main__":
    main()
  