#!/usr/bin/env python3
"""
Generate UPDATE statements that fix HAMOS Agreement.ExternalAgreementId based on
the Gemini matrikkel -> agreementId mapping.

Inputs:
    - CUSTOMER_CSV: headerless export of
        SELECT GPSLSCustomerId, PASystem, AgreementId, ExternalAgreementId,
               GnrBnrFnrSnr, ...
        FROM [HAMOS].[dbo].[Agreement] WHERE GPSLSCustomerId = 1
    - GEMINI_CSV: headerless MatrikkelId,ExternalId lookup from Gemini.

Output:
    - agreement_diff_update_<date>.sql: one `UPDATE Agreement ...` per line
      followed by `GO`, only for agreements whose current ExternalAgreementId
      differs from the Gemini one.
    - agreement_diff_ambiguous_<date>.csv: Gemini matrikkel ids that map to
      more than one distinct ExternalId (skipped from the SQL updates).
    - agreement_diff_ambiguous_gemini_<date>.csv: Gemini Prod lookup for those
      ambiguous matrikkels as matrikkel_id,agreementId,propertyId.
"""
from __future__ import annotations

import csv
from collections import defaultdict
from datetime import date
from pathlib import Path
from typing import Any, Iterable, NamedTuple

import requests

from gemini_diff_report import APIS, fetch_agreements

def parse_matrikkel(value: str) -> tuple[str, str, str, str]:
    """Parse '6.33.0.0' into (gnr, bnr, fnr, snr)."""
    value = value.strip()
    parts = value.split(".")
    if len(parts) != 4:
        raise ValueError(
            f"Matrikkel id must have 4 parts gnr.bnr.fnr.snr, got: {value!r}"
        )
    gnr, bnr, fnr, snr = (p.strip() for p in parts)
    if not all(p.isdigit() for p in (gnr, bnr, fnr, snr)):
        raise ValueError(f"Matrikkel parts must be integers, got: {value!r}")
    return gnr, bnr, fnr, snr

# ========= CONFIG (edit these) =========
CUSTOMER_CSV = r"E:\Temp\Ymir_Compare\customer_1_all_agreements_20260824.csv"
GEMINI_CSV = r"E:\Temp\Ymir_Compare\matrikkel_gemini_agreements_20260824.csv"
OUTPUT_DIR = r"E:\Temp\Ymir_Compare\Sql_Updates"

GPSLS_CUSTOMER_ID = 1
SQL_TABLE_NAME = "Agreement"

# Customer SQL export has no header. Column indexes match the SELECT list.
CUSTOMER_GPSLS_INDEX = 0
CUSTOMER_PASYSTEM_INDEX = 1
CUSTOMER_AGREEMENT_ID_INDEX = 2
CUSTOMER_EXTERNAL_AGREEMENT_ID_INDEX = 3
CUSTOMER_MATRIKKEL_INDEX = 4
# ======================================


class CustomerRow(NamedTuple):
    pa_system: str
    agreement_id: str
    external_agreement_id: str
    matrikkel_id: str


def cell(row: list[str], index: int) -> str:
    if index >= len(row):
        return ""
    return row[index].strip()


def is_missing(value: str) -> bool:
    return not value or value.upper() == "NULL"


def normalize_matrikkel(raw: str) -> str:
    gnr, bnr, fnr, snr = parse_matrikkel(raw)
    return f"{gnr}.{bnr}.{fnr}.{snr}"


def matrikkel_sort_key(matrikkel_id: str) -> tuple[int, int, int, int]:
    gnr, bnr, fnr, snr = parse_matrikkel(matrikkel_id)
    return int(gnr), int(bnr), int(fnr), int(snr)


def agreement_sort_key(agreement_id: str) -> tuple[int, int | str]:
    if agreement_id.isdigit():
        return (0, int(agreement_id))
    return (1, agreement_id)


def sql_escape(value: str) -> str:
    return value.replace("'", "''")


def load_gemini_external_ids(
    csv_path: Path,
) -> tuple[dict[str, str], dict[str, set[str]]]:
    """Return (matrikkel_id -> external_id, ambiguous matrikkel -> external ids).

    Matrikkels that map to more than one distinct ExternalId are excluded from
    the lookup and returned in the ambiguous mapping instead.
    """
    candidates: dict[str, set[str]] = defaultdict(set)
    invalid = 0

    with csv_path.open(newline="", encoding="utf-8-sig") as f:
        reader = csv.reader(f)
        for row in reader:
            if len(row) < 2:
                invalid += 1
                continue
            raw_matrikkel = row[0].strip()
            external_id = row[1].strip()
            if is_missing(raw_matrikkel) or is_missing(external_id):
                invalid += 1
                continue
            try:
                matrikkel_id = normalize_matrikkel(raw_matrikkel)
            except ValueError:
                invalid += 1
                continue
            candidates[matrikkel_id].add(external_id)

    if invalid:
        print(f"Skipped {invalid} invalid Gemini rows")

    lookup: dict[str, str] = {}
    ambiguous: dict[str, set[str]] = {}
    for matrikkel_id, external_ids in candidates.items():
        if len(external_ids) == 1:
            lookup[matrikkel_id] = next(iter(external_ids))
        else:
            ambiguous[matrikkel_id] = external_ids

    return lookup, ambiguous


def load_customer_rows(csv_path: Path) -> list[CustomerRow]:
    rows: list[CustomerRow] = []
    invalid = 0

    with csv_path.open(newline="", encoding="utf-8-sig") as f:
        reader = csv.reader(f)
        for row in reader:
            if len(row) <= CUSTOMER_MATRIKKEL_INDEX:
                invalid += 1
                continue

            pa_system = cell(row, CUSTOMER_PASYSTEM_INDEX)
            agreement_id = cell(row, CUSTOMER_AGREEMENT_ID_INDEX)
            external_id = cell(row, CUSTOMER_EXTERNAL_AGREEMENT_ID_INDEX)
            raw_matrikkel = cell(row, CUSTOMER_MATRIKKEL_INDEX)

            if is_missing(pa_system) or is_missing(agreement_id) or is_missing(raw_matrikkel):
                invalid += 1
                continue
            try:
                matrikkel_id = normalize_matrikkel(raw_matrikkel)
            except ValueError:
                invalid += 1
                continue

            rows.append(
                CustomerRow(
                    pa_system=pa_system,
                    agreement_id=agreement_id,
                    external_agreement_id="" if is_missing(external_id) else external_id,
                    matrikkel_id=matrikkel_id,
                )
            )

    if invalid:
        print(f"Skipped {invalid} invalid customer rows")

    return rows


def generate_update_statements(
    customer_rows: list[CustomerRow],
    gemini_lookup: dict[str, str],
    ambiguous: dict[str, set[str]],
) -> tuple[list[tuple[CustomerRow, str]], dict[str, int]]:
    counters: dict[str, int] = {
        "customer_rows": len(customer_rows),
        "no_gemini_match": 0,
        "ambiguous": 0,
        "already_correct": 0,
        "filled_null": 0,
        "changed": 0,
    }

    updates: list[tuple[CustomerRow, str]] = []

    for row in customer_rows:
        if row.matrikkel_id in ambiguous:
            counters["ambiguous"] += 1
            continue

        gemini_external = gemini_lookup.get(row.matrikkel_id)
        if gemini_external is None:
            counters["no_gemini_match"] += 1
            continue

        if row.external_agreement_id == gemini_external:
            counters["already_correct"] += 1
            continue

        if row.external_agreement_id == "":
            counters["filled_null"] += 1
        else:
            counters["changed"] += 1

        updates.append((row, gemini_external))

    updates.sort(
        key=lambda item: (
            matrikkel_sort_key(item[0].matrikkel_id),
            agreement_sort_key(item[0].agreement_id),
        )
    )

    return updates, counters


def build_sql(row: CustomerRow, new_external_id: str) -> str:
    return (
        f"UPDATE {SQL_TABLE_NAME} "
        f"SET ExternalAgreementId = '{sql_escape(new_external_id)}' "
        f"WHERE GPSLSCustomerId = {GPSLS_CUSTOMER_ID} "
        f"AND PASystem = '{sql_escape(row.pa_system)}' "
        f"AND AgreementId = {row.agreement_id};"
    )


def write_sql(path: Path, updates: list[tuple[CustomerRow, str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as f:
        for row, new_external in updates:
            f.write(build_sql(row, new_external) + "\n")
            f.write("GO\n")


def write_ambiguous(path: Path, ambiguous: dict[str, set[str]]) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    rows: list[tuple[str, str]] = []
    for matrikkel_id, external_ids in ambiguous.items():
        for external_id in sorted(external_ids, key=agreement_sort_key):
            rows.append((matrikkel_id, external_id))
    rows.sort(key=lambda item: (matrikkel_sort_key(item[0]), agreement_sort_key(item[1])))

    with path.open("w", newline="", encoding="utf-8") as f:
        writer = csv.writer(f, lineterminator="\n")
        writer.writerow(["MatrikkelId", "GeminiExternalId"])
        writer.writerows(rows)


def records_from_gemini_payload(payload: Any) -> list[tuple[str, str]]:
    """Return unique (agreementId, propertyId) pairs from a Gemini search payload."""
    if not isinstance(payload, list):
        return []

    records: list[tuple[str, str]] = []
    seen: set[tuple[str, str]] = set()
    for item in payload:
        if not isinstance(item, dict):
            continue
        agreement_id = item.get("agreementId")
        if agreement_id is None or not str(agreement_id).strip():
            continue
        agreement_id = str(agreement_id).strip()
        property_id = item.get("propertyId")
        property_id = "" if property_id is None else str(property_id).strip()
        key = (agreement_id, property_id)
        if key in seen:
            continue
        seen.add(key)
        records.append(key)
    return records


def download_ambigous_gemini_data(
    matrikkel_ids: Iterable[str], output_path: Path
) -> None:
    """Fetch Gemini Prod agreements for ambiguous matrikkels, including propertyId.

    Writes headerless CSV rows: matrikkel_id,agreementId,propertyId.
    """
    output_path.parent.mkdir(parents=True, exist_ok=True)
    prod_api = APIS["prod"]
    pair_count = 0
    missing_count = 0
    error_count = 0

    with output_path.open("w", newline="", encoding="utf-8") as out:
        writer = csv.writer(out, delimiter=",", lineterminator="\n")
        for matrikkel_id in sorted(matrikkel_ids, key=matrikkel_sort_key):
            gnr, bnr, fnr, snr = parse_matrikkel(matrikkel_id)
            try:
                payload = fetch_agreements(prod_api, gnr, bnr, fnr, snr)
            except requests.RequestException as ex:
                error_count += 1
                print(f"{matrikkel_id}: skipped (lookup failed): {ex}")
                continue

            records = records_from_gemini_payload(payload)
            if not records:
                missing_count += 1
                print(f"{matrikkel_id}: no Gemini agreement ids")
                continue

            records.sort(
                key=lambda item: (
                    agreement_sort_key(item[0]),
                    agreement_sort_key(item[1]),
                )
            )
            for agreement_id, property_id in records:
                writer.writerow([matrikkel_id, agreement_id, property_id])
                pair_count += 1
            out.flush()
            print(
                f"{matrikkel_id}: "
                + ", ".join(
                    f"{agreement_id}:{property_id}" if property_id else agreement_id
                    for agreement_id, property_id in records
                )
            )

    print(
        f"Saved {pair_count} Gemini rows to {output_path} "
        f"(missing={missing_count}, errors={error_count})"
    )


def main() -> None:
    customer_path = Path(CUSTOMER_CSV)
    gemini_path = Path(GEMINI_CSV)
    if not customer_path.is_file():
        raise FileNotFoundError(f"Customer CSV not found: {customer_path}")
    if not gemini_path.is_file():
        raise FileNotFoundError(f"Gemini CSV not found: {gemini_path}")

    gemini_lookup, ambiguous = load_gemini_external_ids(gemini_path)
    customer_rows = load_customer_rows(customer_path)
    print(f"Loaded {len(customer_rows)} customer rows")
    print(
        f"Loaded {len(gemini_lookup)} unique Gemini matrikkels "
        f"({len(ambiguous)} ambiguous, skipped)"
    )

    updates, counters = generate_update_statements(
        customer_rows, gemini_lookup, ambiguous
    )

    today = date.today().strftime("%Y%m%d")
    output_dir = Path(OUTPUT_DIR)
    output_path = output_dir / f"agreement_diff_update_{today}.sql"
    ambiguous_path = output_dir / f"agreement_diff_ambiguous_{today}.csv"
    write_sql(output_path, updates)
    write_ambiguous(ambiguous_path, ambiguous)

    print(f"Customer rows:      {counters['customer_rows']}")
    print(f"No Gemini match:    {counters['no_gemini_match']}")
    print(f"Ambiguous Gemini:   {counters['ambiguous']}")
    print(f"Already correct:    {counters['already_correct']}")
    print(f"Filled NULL:        {counters['filled_null']}")
    print(f"Changed value:      {counters['changed']}")
    print(f"Saved {len(updates)} UPDATE statements to {output_path}")
    print(
        f"Saved {len(ambiguous)} ambiguous Gemini matrikkels to {ambiguous_path}"
    )

    gemini_detail_path = output_dir / f"agreement_diff_ambiguous_gemini_{today}.csv"
    download_ambigous_gemini_data(ambiguous, gemini_detail_path)


if __name__ == "__main__":
    main()
