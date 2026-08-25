#!/usr/bin/env python3
"""
Read matrikkel ids from the customer agreements CSV, look up Gemini Prod
agreement ids, and write MatrikkelId,GeminiAgreementId pairs to a CSV.
"""
from __future__ import annotations

import csv
from pathlib import Path

from gemini_diff_report import APIS, fetch_agreement_ids, parse_matrikkel

# ========= CONFIG (edit these) =========
INPUT_CSV = r"E:\Temp\Ymir_Compare\customer_1_agreements_with_registryid_20260825.csv"
OUTPUT_DIR = r"E:\Temp\Ymir_Compare"

# Slice of unique, ascending matrikkel ids. END_INDEX is exclusive.
# Example: 0, 1000 downloads the first 1000 ids. Set END_INDEX to None for the rest.
START_INDEX = 0
END_INDEX = 20000

# CSV has no header. Columns are:
# GPSLSCustomerId, PASystem, AgreementId, ExternalAgreementId, GnrBnrFnrSnr,
# Bid, BuildingType, NrOfOccupancyUnits, RegDate, Type, Name, Address1,
# LastChanged, RegistryId. GnrBnrFnrSnr is the 5th column (0-based index 4).
MATRIKKEL_COLUMN_INDEX = 4
# ======================================


def matrikkel_sort_key(matrikkel_id: str) -> tuple[int, int, int, int]:
    gnr, bnr, fnr, snr = parse_matrikkel(matrikkel_id)
    return int(gnr), int(bnr), int(fnr), int(snr)


def load_unique_matrikkel_ids(csv_path: Path) -> list[str]:
    """Read unique matrikkel ids from the SQL export CSV and sort them ascending."""
    seen: set[str] = set()
    invalid = 0

    with csv_path.open(newline="", encoding="utf-8-sig") as f:
        reader = csv.reader(f)
        for row in reader:
            if len(row) <= MATRIKKEL_COLUMN_INDEX:
                invalid += 1
                continue
            raw = row[MATRIKKEL_COLUMN_INDEX].strip()
            if not raw or raw.upper() == "NULL":
                invalid += 1
                continue
            try:
                gnr, bnr, fnr, snr = parse_matrikkel(raw)
            except ValueError:
                invalid += 1
                continue
            seen.add(f"{gnr}.{bnr}.{fnr}.{snr}")

    if invalid:
        print(f"Skipped {invalid} rows with missing/invalid matrikkel id")

    return sorted(seen, key=matrikkel_sort_key)


def agreement_sort_key(agreement_id: str) -> tuple[int, int | str]:
    if agreement_id.isdigit():
        return (0, int(agreement_id))
    return (1, agreement_id)


def slice_matrikkel_ids(matrikkel_ids: list[str], start: int, end: int | None) -> list[str]:
    if start < 0:
        raise ValueError(f"START_INDEX must be >= 0, got {start}")
    if end is not None and end < start:
        raise ValueError(f"END_INDEX ({end}) must be >= START_INDEX ({start})")
    return matrikkel_ids[start:end]


def output_filename(start: int, end: int | None, total: int) -> str:
    end_label = total if end is None else min(end, total)
    return f"matrikkel_gemini_agreements_{start}_{end_label}.csv"


def main() -> None:
    input_path = Path(INPUT_CSV)
    if not input_path.is_file():
        raise FileNotFoundError(f"Input CSV not found: {input_path}")

    matrikkel_ids = load_unique_matrikkel_ids(input_path)
    batch = slice_matrikkel_ids(matrikkel_ids, START_INDEX, END_INDEX)
    actual_end = START_INDEX + len(batch)

    print(f"Loaded {len(matrikkel_ids)} unique matrikkel ids")
    print(f"Fetching Prod agreements for indexes [{START_INDEX}, {actual_end})")

    output_dir = Path(OUTPUT_DIR)
    output_dir.mkdir(parents=True, exist_ok=True)
    output_path = output_dir / output_filename(START_INDEX, END_INDEX, len(matrikkel_ids))

    prod_api = APIS["prod"]
    pair_count = 0
    missing_count = 0
    error_count = 0

    with output_path.open("w", newline="", encoding="utf-8") as out:
        writer = csv.writer(out, delimiter=",", lineterminator="\n")
        for offset, matrikkel_id in enumerate(batch):
            index = START_INDEX + offset
            gnr, bnr, fnr, snr = parse_matrikkel(matrikkel_id)
            agreement_ids = fetch_agreement_ids(prod_api, gnr, bnr, fnr, snr)

            if agreement_ids is None:
                error_count += 1
                print(f"{index}: {matrikkel_id}: skipped (lookup failed)")
                continue

            if not agreement_ids:
                missing_count += 1
                print(f"{index}: {matrikkel_id}: no Gemini agreement ids")
                continue

            for agreement_id in sorted(agreement_ids, key=agreement_sort_key):
                writer.writerow([matrikkel_id, agreement_id])
                pair_count += 1
            out.flush()
            print(
                f"{index}: {matrikkel_id}: "
                f"{', '.join(sorted(agreement_ids, key=agreement_sort_key))}"
            )

    print(
        f"Saved {pair_count} pairs to {output_path} "
        f"(missing={missing_count}, errors={error_count})"
    )


if __name__ == "__main__":
    main()
