#!/usr/bin/env python3
"""
Generate UPDATE statements that fix HAMOS Agreement.ExternalAgreementId based on
the Gemini matrikkel -> agreementId mapping.

Inputs:
    - CUSTOMER_CSV: headerless export of
        SELECT GPSLSCustomerId, PASystem, GnrBnrFnrSnr, Gid, AgreementId,
               ExternalAgreementId, Bid, RegDate, Type, ContactPerson, Estate,
               EstatePostalCode, Address1, Address2, CustomerId, Name, Aid,
               PostalCode, Commune, LastChanged, Status, Termin, BuildingType,
               RegistryId, OwnerId, NrOfOccupancyUnits
        FROM [HAMOS].[dbo].[Agreement]
        WHERE GPSLSCustomerId = 1
          AND (Type = 'Eg' OR Type = '12t' OR Type = 'BBL' OR Type = 'Fr')
        ORDER BY Type
    - GEMINI_CSV: headerless MatrikkelId,ExternalId lookup from Gemini.

Output:
    - agreement_diff_update_<date>.sql: one `UPDATE Agreement ...` per line
      followed by `GO`, only for non-ambiguous matrikkels whose current
      ExternalAgreementId differs from the Gemini one.
    - agreement_diff_ambiguous_<date>.csv: Gemini matrikkel ids that map to
      more than one distinct ExternalId (skipped from the SQL updates).
    - agreement_diff_ambiguous_gemini_<date>.json: Gemini Prod lookup for those
      ambiguous matrikkels as objects with matrikkelId, agreementId,
      propertyId, agreementDescription and agreementText.
    - agreement_diff_ambiguous_combined_<date>.json: the ambiguous Gemini
      agreements grouped by matrikkelId, each with the Fieldata agreements from
      CUSTOMER_CSV that share the same GnrBnrFnrSnr. Matrikkels without any
      Fieldata agreement are left out.
"""
from __future__ import annotations

import csv
import json
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
CUSTOMER_CSV = r"E:\Temp\Ymir_Compare\customer_1_agreements__20260826.csv"
GEMINI_CSV = r"E:\Temp\Ymir_Compare\matrikkel_gemini_agreements_20260824.csv"
OUTPUT_DIR = r"E:\Temp\Ymir_Compare\Sql_Updates"

DOWNLOAD_AMBIGUOUS_GEMINI = False
BUILD_AMBIGUOUS_COMBINED = True

# Blank = newest agreement_diff_ambiguous_gemini_*.json found in OUTPUT_DIR.
AMBIGUOUS_GEMINI_JSON = ""

GPSLS_CUSTOMER_ID = 1
SQL_TABLE_NAME = "Agreement"

# Customer SQL export has no header. Column indexes match the SELECT list.
CUSTOMER_GPSLS_INDEX = 0
CUSTOMER_PASYSTEM_INDEX = 1
CUSTOMER_MATRIKKEL_INDEX = 2
CUSTOMER_GID_INDEX = 3
CUSTOMER_AGREEMENT_ID_INDEX = 4
CUSTOMER_EXTERNAL_AGREEMENT_ID_INDEX = 5
CUSTOMER_TYPE_INDEX = 8
CUSTOMER_CONTACT_PERSON_INDEX = 9
CUSTOMER_ESTATE_INDEX = 10
CUSTOMER_ESTATE_POSTAL_CODE_INDEX = 11
CUSTOMER_ADDRESS1_INDEX = 12
CUSTOMER_ADDRESS2_INDEX = 13
CUSTOMER_BUILDING_TYPE_INDEX = 22
# ======================================


class CustomerRow(NamedTuple):
    pa_system: str
    agreement_id: str
    external_agreement_id: str
    matrikkel_id: str
    gpsls_customer_id: str = ""
    type: str = ""
    contact_person: str = ""
    estate: str = ""
    estate_postal_code: str = ""
    address1: str = ""
    address2: str = ""
    building_type: str = ""


def cell(row: list[str], index: int) -> str:
    if index >= len(row):
        return ""
    return row[index].strip()


def is_missing(value: str) -> bool:
    return not value or value.upper() == "NULL"


def optional_cell(row: list[str], index: int) -> str:
    value = cell(row, index)
    return "" if is_missing(value) else value


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
            if len(row) <= CUSTOMER_EXTERNAL_AGREEMENT_ID_INDEX:
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
                    gpsls_customer_id=optional_cell(row, CUSTOMER_GPSLS_INDEX),
                    type=optional_cell(row, CUSTOMER_TYPE_INDEX),
                    contact_person=optional_cell(row, CUSTOMER_CONTACT_PERSON_INDEX),
                    estate=optional_cell(row, CUSTOMER_ESTATE_INDEX),
                    estate_postal_code=optional_cell(
                        row, CUSTOMER_ESTATE_POSTAL_CODE_INDEX
                    ),
                    address1=optional_cell(row, CUSTOMER_ADDRESS1_INDEX),
                    address2=optional_cell(row, CUSTOMER_ADDRESS2_INDEX),
                    building_type=optional_cell(row, CUSTOMER_BUILDING_TYPE_INDEX),
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


def records_from_gemini_payload(
    matrikkel_id: str, payload: Any
) -> list[dict[str, Any]]:
    """Map a Gemini search payload to agreement records for the JSON output."""
    if not isinstance(payload, list):
        return []

    records: list[dict[str, Any]] = []
    for item in payload:
        if not isinstance(item, dict):
            continue
        agreement_id = item.get("agreementId")
        if agreement_id is None or not str(agreement_id).strip():
            continue
        records.append(
            {
                "matrikkelId": matrikkel_id,
                "agreementId": str(agreement_id).strip(),
                "propertyId": item.get("propertyId"),
                "agreementDescription": item.get("agreementDescription"),
                "agreementText": item.get("agreementText"),
            }
        )
    return records


def download_ambigous_gemini_data(
    matrikkel_ids: Iterable[str], output_path: Path
) -> None:
    """Fetch Gemini Prod agreements for ambiguous matrikkels into a JSON file.

    Writes a JSON array of objects with matrikkelId, agreementId, propertyId,
    agreementDescription and agreementText.
    """
    output_path.parent.mkdir(parents=True, exist_ok=True)
    prod_api = APIS["prod"]
    records: list[dict[str, Any]] = []
    missing_count = 0
    error_count = 0

    for matrikkel_id in sorted(matrikkel_ids, key=matrikkel_sort_key):
        gnr, bnr, fnr, snr = parse_matrikkel(matrikkel_id)
        try:
            payload = fetch_agreements(prod_api, gnr, bnr, fnr, snr)
        except requests.RequestException as ex:
            error_count += 1
            print(f"{matrikkel_id}: skipped (lookup failed): {ex}")
            continue

        matrikkel_records = records_from_gemini_payload(matrikkel_id, payload)
        if not matrikkel_records:
            missing_count += 1
            print(f"{matrikkel_id}: no Gemini agreement ids")
            continue

        matrikkel_records.sort(
            key=lambda item: agreement_sort_key(item["agreementId"])
        )
        records.extend(matrikkel_records)
        print(
            f"{matrikkel_id}: "
            + ", ".join(item["agreementId"] for item in matrikkel_records)
        )

    output_path.write_text(
        json.dumps(records, indent=4, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )

    print(
        f"Saved {len(records)} Gemini records to {output_path} "
        f"(missing={missing_count}, errors={error_count})"
    )


def resolve_ambiguous_gemini_path(output_dir: Path, today: str) -> Path | None:
    """Pick the Gemini ambiguous JSON to join with, newest date wins."""
    if AMBIGUOUS_GEMINI_JSON:
        configured = Path(AMBIGUOUS_GEMINI_JSON)
        return configured if configured.is_file() else None

    todays_path = output_dir / f"agreement_diff_ambiguous_gemini_{today}.json"
    if todays_path.is_file():
        return todays_path

    candidates = sorted(output_dir.glob("agreement_diff_ambiguous_gemini_*.json"))
    return candidates[-1] if candidates else None


def load_ambiguous_gemini_records(
    output_dir: Path, today: str, ambiguous: Iterable[str]
) -> list[dict[str, Any]]:
    """Read the ambiguous Gemini agreements, downloading them if none exist."""
    path = resolve_ambiguous_gemini_path(output_dir, today)
    if path is None:
        path = output_dir / f"agreement_diff_ambiguous_gemini_{today}.json"
        print(f"No Gemini ambiguous JSON found, downloading to {path}")
        download_ambigous_gemini_data(ambiguous, path)

    records = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(records, list):
        raise ValueError(f"Expected a JSON array in {path}")

    print(f"Loaded {len(records)} ambiguous Gemini records from {path}")
    return [item for item in records if isinstance(item, dict)]


def build_ambiguous_combined_json(
    gemini_records: list[dict[str, Any]],
    customer_rows: list[CustomerRow],
    output_path: Path,
) -> None:
    """Group the ambiguous Gemini agreements per matrikkel with Fieldata rows."""
    gemini_by_matrikkel: dict[str, list[dict[str, Any]]] = defaultdict(list)
    for record in gemini_records:
        matrikkel_id = str(record.get("matrikkelId") or "").strip()
        if not matrikkel_id:
            continue
        gemini_by_matrikkel[matrikkel_id].append(
            {
                "agreementId": record.get("agreementId"),
                "agreementDescription": record.get("agreementDescription"),
                "agreementText": record.get("agreementText"),
            }
        )

    fieldata_by_matrikkel: dict[str, list[CustomerRow]] = defaultdict(list)
    for row in customer_rows:
        fieldata_by_matrikkel[row.matrikkel_id].append(row)

    entries: list[dict[str, Any]] = []
    gemini_count = 0
    fieldata_count = 0
    skipped_without_fieldata = 0

    for matrikkel_id in sorted(gemini_by_matrikkel, key=matrikkel_sort_key):
        fieldata_rows = sorted(
            fieldata_by_matrikkel.get(matrikkel_id, []),
            key=lambda row: agreement_sort_key(row.agreement_id),
        )
        if not fieldata_rows:
            skipped_without_fieldata += 1
            continue

        gemini_agreements = sorted(
            gemini_by_matrikkel[matrikkel_id],
            key=lambda item: agreement_sort_key(str(item["agreementId"] or "")),
        )

        gemini_count += len(gemini_agreements)
        fieldata_count += len(fieldata_rows)

        entries.append(
            {
                "matrikkelId": matrikkel_id,
                "geminiAgreements": gemini_agreements,
                "fieldataAgreements": [
                    fieldata_agreement(row) for row in fieldata_rows
                ],
            }
        )

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(
        json.dumps(entries, indent=4, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )

    print(
        f"Saved {len(entries)} matrikkels to {output_path} "
        f"(gemini={gemini_count}, fieldata={fieldata_count}, "
        f"skipped without fieldata={skipped_without_fieldata})"
    )


def fieldata_agreement(row: CustomerRow) -> dict[str, Any]:
    return {
        "gpslsCustomerId": row.gpsls_customer_id or None,
        "paSystem": row.pa_system or None,
        "gnrBnrFnrSnr": row.matrikkel_id or None,
        "agreementId": row.agreement_id or None,
        "externalAgreementId": row.external_agreement_id or None,
        "type": row.type or None,
        "contactPerson": row.contact_person or None,
        "estate": row.estate or None,
        "estatePostalCode": row.estate_postal_code or None,
        "address1": row.address1 or None,
        "address2": row.address2 or None,
        "buildingType": row.building_type or None,
    }


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

    if DOWNLOAD_AMBIGUOUS_GEMINI:
        gemini_detail_path = output_dir / f"agreement_diff_ambiguous_gemini_{today}.json"
        download_ambigous_gemini_data(ambiguous, gemini_detail_path)

    if BUILD_AMBIGUOUS_COMBINED:
        combined_path = output_dir / f"agreement_diff_ambiguous_combined_{today}.json"
        gemini_records = load_ambiguous_gemini_records(output_dir, today, ambiguous)
        build_ambiguous_combined_json(gemini_records, customer_rows, combined_path)


if __name__ == "__main__":
    main()
