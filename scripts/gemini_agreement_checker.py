#!/usr/bin/env python3
"""
Compare Gemini matrikkel/agreement pairs with the HAMOS customer export.

Gemini CSV lines are: matrikkel_id,agreement_id
Customer CSV is the GPSLSCustomerId=1 Agreement export (no header).
Gemini agreement_id is checked against ExternalAgreementId, then matrikkel_id
is checked against GnrBnrFnrSnr. Also reports unique GnrBnrFnrSnr values from
the SQL export that never appear in the Gemini CSV, and writes those matrikkel
ids to a separate file.
"""
from __future__ import annotations

import csv
from collections import defaultdict
from pathlib import Path
from typing import NamedTuple

from gemini_diff_report import parse_matrikkel

# ========= CONFIG (edit these) =========
GEMINI_CSV = r"E:\Temp\Ymir_Compare\matrikkel_gemini_agreements_20260824.csv"
CUSTOMER_CSV = r"E:\Temp\Ymir_Compare\customer_1_agreements_with_registryid_20260825.csv"
OUTPUT_DIR = r"E:\Temp\Ymir_Compare"

# Customer SQL export has no header. Column indexes match:
# GPSLSCustomerId, PASystem, AgreementId, ExternalAgreementId, GnrBnrFnrSnr,
# Bid, BuildingType, NrOfOccupancyUnits, RegDate, Type, Name, Address1,
# LastChanged, RegistryId
CUSTOMER_AGREEMENT_ID_INDEX = 2
CUSTOMER_EXTERNAL_AGREEMENT_ID_INDEX = 3
CUSTOMER_MATRIKKEL_INDEX = 4
CUSTOMER_NAME_INDEX = 10
CUSTOMER_ADDRESS1_INDEX = 11
# ======================================

MISMATCH_MISSING_IN_CUSTOMER = "missing_in_customer"
MISMATCH_MISSING_IN_GEMINI = "missing_in_gemini"
MISMATCH_MATRIKKEL = "matrikkel_mismatch"
MISMATCH_NULL_EXTERNAL = "null_external_id"
MISMATCH_MATRIKKEL_ONLY_IN_CUSTOMER = "matrikkel_missing_in_gemini"

OUTPUT_COLUMNS = [
    "MismatchType",
    "GeminiMatrikkelId",
    "GeminiAgreementId",
    "CustomerMatrikkelId",
    "CustomerAgreementId",
    "ExternalAgreementId",
    "Name",
    "Address1",
]


class GeminiPair(NamedTuple):
    matrikkel_id: str
    agreement_id: str


class CustomerRow(NamedTuple):
    agreement_id: str
    external_agreement_id: str
    matrikkel_id: str
    name: str
    address1: str


def matrikkel_sort_key(matrikkel_id: str) -> tuple[int, int, int, int]:
    gnr, bnr, fnr, snr = parse_matrikkel(matrikkel_id)
    return int(gnr), int(bnr), int(fnr), int(snr)


def agreement_sort_key(agreement_id: str) -> tuple[int, int | str]:
    if agreement_id.isdigit():
        return (0, int(agreement_id))
    return (1, agreement_id)


def normalize_matrikkel(raw: str) -> str:
    gnr, bnr, fnr, snr = parse_matrikkel(raw)
    return f"{gnr}.{bnr}.{fnr}.{snr}"


def cell(row: list[str], index: int) -> str:
    if index >= len(row):
        return ""
    return row[index].strip()


def is_missing(value: str) -> bool:
    return not value or value.upper() == "NULL"


def load_gemini_pairs(csv_path: Path) -> list[GeminiPair]:
    """Read matrikkel_id,agreement_id pairs from the Gemini lookup CSV."""
    pairs: list[GeminiPair] = []
    seen: set[tuple[str, str]] = set()
    invalid = 0

    with csv_path.open(newline="", encoding="utf-8-sig") as f:
        reader = csv.reader(f)
        for row in reader:
            if len(row) < 2:
                invalid += 1
                continue
            raw_matrikkel = row[0].strip()
            agreement_id = row[1].strip()
            if is_missing(raw_matrikkel) or is_missing(agreement_id):
                invalid += 1
                continue
            try:
                matrikkel_id = normalize_matrikkel(raw_matrikkel)
            except ValueError:
                invalid += 1
                continue
            key = (matrikkel_id, agreement_id)
            if key in seen:
                continue
            seen.add(key)
            pairs.append(GeminiPair(matrikkel_id, agreement_id))

    if invalid:
        print(f"Skipped {invalid} invalid Gemini rows")

    return pairs


def load_customer_rows(csv_path: Path) -> tuple[list[CustomerRow], list[CustomerRow]]:
    """Read HAMOS Agreement rows. Returns (rows with external id, rows with null external id)."""
    rows: list[CustomerRow] = []
    null_external: list[CustomerRow] = []
    invalid = 0

    with csv_path.open(newline="", encoding="utf-8-sig") as f:
        reader = csv.reader(f)
        for row in reader:
            if len(row) <= CUSTOMER_MATRIKKEL_INDEX:
                invalid += 1
                continue

            agreement_id = cell(row, CUSTOMER_AGREEMENT_ID_INDEX)
            external_id = cell(row, CUSTOMER_EXTERNAL_AGREEMENT_ID_INDEX)
            raw_matrikkel = cell(row, CUSTOMER_MATRIKKEL_INDEX)
            name = cell(row, CUSTOMER_NAME_INDEX)
            address1 = cell(row, CUSTOMER_ADDRESS1_INDEX)

            if is_missing(raw_matrikkel):
                invalid += 1
                continue
            try:
                matrikkel_id = normalize_matrikkel(raw_matrikkel)
            except ValueError:
                invalid += 1
                continue

            customer = CustomerRow(
                agreement_id=agreement_id,
                external_agreement_id=external_id,
                matrikkel_id=matrikkel_id,
                name="" if is_missing(name) else name,
                address1="" if is_missing(address1) else address1,
            )
            if is_missing(external_id):
                null_external.append(customer)
            else:
                rows.append(customer)

    if invalid:
        print(f"Skipped {invalid} invalid customer rows")

    return rows, null_external


def mismatch_row(
    mismatch_type: str,
    *,
    gemini: GeminiPair | None = None,
    customer: CustomerRow | None = None,
) -> dict[str, str]:
    return {
        "MismatchType": mismatch_type,
        "GeminiMatrikkelId": gemini.matrikkel_id if gemini else "",
        "GeminiAgreementId": gemini.agreement_id if gemini else "",
        "CustomerMatrikkelId": customer.matrikkel_id if customer else "",
        "CustomerAgreementId": customer.agreement_id if customer else "",
        "ExternalAgreementId": customer.external_agreement_id if customer else "",
        "Name": customer.name if customer else "",
        "Address1": customer.address1 if customer else "",
    }


def compare(
    gemini_pairs: list[GeminiPair],
    customer_rows: list[CustomerRow],
    null_external: list[CustomerRow],
) -> tuple[list[dict[str, str]], dict[str, int]]:
    customer_by_external: dict[str, list[CustomerRow]] = defaultdict(list)
    for row in customer_rows:
        customer_by_external[row.external_agreement_id].append(row)

    gemini_by_agreement: dict[str, list[GeminiPair]] = defaultdict(list)
    for pair in gemini_pairs:
        gemini_by_agreement[pair.agreement_id].append(pair)

    mismatches: list[dict[str, str]] = []
    matched = 0

    for pair in gemini_pairs:
        customers = customer_by_external.get(pair.agreement_id)
        if not customers:
            mismatches.append(
                mismatch_row(MISMATCH_MISSING_IN_CUSTOMER, gemini=pair)
            )
            continue

        matching = [row for row in customers if row.matrikkel_id == pair.matrikkel_id]
        if matching:
            matched += 1
            continue

        for row in customers:
            mismatches.append(
                mismatch_row(MISMATCH_MATRIKKEL, gemini=pair, customer=row)
            )

    for row in customer_rows:
        gemini_for_id = gemini_by_agreement.get(row.external_agreement_id)
        if not gemini_for_id:
            mismatches.append(
                mismatch_row(MISMATCH_MISSING_IN_GEMINI, customer=row)
            )
            continue
        if any(pair.matrikkel_id == row.matrikkel_id for pair in gemini_for_id):
            continue
        # Matrikkel mismatches are already recorded from the Gemini side.

    for row in null_external:
        mismatches.append(mismatch_row(MISMATCH_NULL_EXTERNAL, customer=row))

    gemini_matrikkels = {pair.matrikkel_id for pair in gemini_pairs}
    all_customer_rows = customer_rows + null_external
    customer_by_matrikkel: dict[str, list[CustomerRow]] = defaultdict(list)
    for row in all_customer_rows:
        customer_by_matrikkel[row.matrikkel_id].append(row)

    missing_matrikkels = sorted(
        set(customer_by_matrikkel) - gemini_matrikkels,
        key=matrikkel_sort_key,
    )
    missing_matrikkel_rows = 0
    for matrikkel_id in missing_matrikkels:
        rows_for_matrikkel = customer_by_matrikkel[matrikkel_id]
        missing_matrikkel_rows += len(rows_for_matrikkel)
        sample = min(rows_for_matrikkel, key=lambda row: agreement_sort_key(row.agreement_id))
        mismatches.append(
            mismatch_row(MISMATCH_MATRIKKEL_ONLY_IN_CUSTOMER, customer=sample)
        )

    counts = {
        "gemini_pairs": len(gemini_pairs),
        "customer_rows": len(all_customer_rows),
        "unique_gemini_matrikkels": len(gemini_matrikkels),
        "unique_customer_matrikkels": len(customer_by_matrikkel),
        "matched": matched,
        "matrikkel_missing_in_gemini_rows": missing_matrikkel_rows,
        MISMATCH_MISSING_IN_CUSTOMER: 0,
        MISMATCH_MISSING_IN_GEMINI: 0,
        MISMATCH_MATRIKKEL: 0,
        MISMATCH_NULL_EXTERNAL: 0,
        MISMATCH_MATRIKKEL_ONLY_IN_CUSTOMER: 0,
    }
    for row in mismatches:
        counts[row["MismatchType"]] += 1

    return mismatches, counts


def sort_mismatches(rows: list[dict[str, str]]) -> list[dict[str, str]]:
    type_order = {
        MISMATCH_MISSING_IN_CUSTOMER: 0,
        MISMATCH_MISSING_IN_GEMINI: 1,
        MISMATCH_MATRIKKEL: 2,
        MISMATCH_NULL_EXTERNAL: 3,
        MISMATCH_MATRIKKEL_ONLY_IN_CUSTOMER: 4,
    }

    def key(row: dict[str, str]) -> tuple:
        matrikkel = row["GeminiMatrikkelId"] or row["CustomerMatrikkelId"]
        agreement = row["GeminiAgreementId"] or row["ExternalAgreementId"]
        try:
            matrikkel_key = matrikkel_sort_key(matrikkel) if matrikkel else (10**9,) * 4
        except ValueError:
            matrikkel_key = (10**9,) * 4
        return (
            type_order.get(row["MismatchType"], 99),
            matrikkel_key,
            agreement_sort_key(agreement) if agreement else (1, ""),
            row["CustomerAgreementId"],
        )

    return sorted(rows, key=key)


def write_mismatches(path: Path, rows: list[dict[str, str]]) -> None:
    with path.open("w", newline="", encoding="utf-8") as out:
        writer = csv.DictWriter(out, fieldnames=OUTPUT_COLUMNS, lineterminator="\n")
        writer.writeheader()
        writer.writerows(rows)


def matrikkel_ids_only_in_customer(
    gemini_pairs: list[GeminiPair],
    customer_rows: list[CustomerRow],
    null_external: list[CustomerRow],
) -> list[str]:
    """Unique GnrBnrFnrSnr values that never appear in the Gemini CSV."""
    gemini_matrikkels = {pair.matrikkel_id for pair in gemini_pairs}
    customer_matrikkels = {row.matrikkel_id for row in customer_rows + null_external}
    return sorted(customer_matrikkels - gemini_matrikkels, key=matrikkel_sort_key)


def write_id_list(path: Path, ids: list[str]) -> None:
    with path.open("w", newline="", encoding="utf-8") as out:
        writer = csv.writer(out, lineterminator="\n")
        writer.writerow(["MatrikkelId"])
        for matrikkel_id in ids:
            writer.writerow([matrikkel_id])


def main() -> None:
    gemini_path = Path(GEMINI_CSV)
    customer_path = Path(CUSTOMER_CSV)
    if not gemini_path.is_file():
        raise FileNotFoundError(f"Gemini CSV not found: {gemini_path}")
    if not customer_path.is_file():
        raise FileNotFoundError(f"Customer CSV not found: {customer_path}")

    gemini_pairs = load_gemini_pairs(gemini_path)
    customer_rows, null_external = load_customer_rows(customer_path)
    print(f"Loaded {len(gemini_pairs)} Gemini pairs")
    print(
        f"Loaded {len(customer_rows) + len(null_external)} customer rows "
        f"({len(null_external)} with null ExternalAgreementId)"
    )

    mismatches, counts = compare(gemini_pairs, customer_rows, null_external)
    mismatches = sort_mismatches(mismatches)

    output_dir = Path(OUTPUT_DIR)
    output_dir.mkdir(parents=True, exist_ok=True)
    output_path = output_dir / "agreement_check_mismatches_20260824.csv"
    write_mismatches(output_path, mismatches)

    only_in_customer_ids = matrikkel_ids_only_in_customer(
        gemini_pairs, customer_rows, null_external
    )
    only_in_customer_path = output_dir / "matrikkel_ids_only_in_customer_20260824.csv"
    write_id_list(only_in_customer_path, only_in_customer_ids)

    print(f"Matched: {counts['matched']}")
    print(f"Missing in customer (Gemini id not in ExternalAgreementId): {counts[MISMATCH_MISSING_IN_CUSTOMER]}")
    print(f"Missing in Gemini (ExternalAgreementId not in Gemini): {counts[MISMATCH_MISSING_IN_GEMINI]}")
    print(f"Matrikkel mismatch (same id, different matrikkel): {counts[MISMATCH_MATRIKKEL]}")
    print(f"Null ExternalAgreementId: {counts[MISMATCH_NULL_EXTERNAL]}")
    print(f"Unique Gemini matrikkels: {counts['unique_gemini_matrikkels']}")
    print(f"Unique customer matrikkels: {counts['unique_customer_matrikkels']}")
    print(
        f"Customer matrikkels not in Gemini: {counts[MISMATCH_MATRIKKEL_ONLY_IN_CUSTOMER]} "
        f"({counts['matrikkel_missing_in_gemini_rows']} agreement rows)"
    )
    print(f"Saved {len(mismatches)} mismatch rows to {output_path}")
    print(
        f"Saved {len(only_in_customer_ids)} matrikkel ids only in customer to "
        f"{only_in_customer_path}"
    )


if __name__ == "__main__":
    main()
