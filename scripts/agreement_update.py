import csv
from time import sleep
import requests
from pathlib import Path
from typing import Optional, Tuple, List, Any, Dict

CSV_PATH = r"E:\Temp\Ymir_sync\Stavanger_Testdata.csv"
OUTPUT_SQL_PATH = r"E:\Temp\Ymir_sync\update_external_agreement.sql"

API_BASE_URL = (
    "https://powelqapfpublicapi.azure-api.net/public/invoicing/api/agreements/search"
)

HEADERS = {
    "municipalityNo": "stavangerkundetest",
    "ocp-apim-subscription-key": "3d8d028ee9be4cc9a9e4ac0a92068966",
}

SQL_TABLE_NAME = "Agreement"

def parse_gnr_bnr_fnr_snr(value: str) -> Tuple[str, str, str, str]:
    """
    Parse a GnrBnrFnrSnr string like '6.657.0.0' into ('6', '657', '0', '0').
    """
    value = value.strip()
    parts = value.split(".")
    if len(parts) != 4:
        raise ValueError(f"Unexpected GnrBnrFnrSnr format: {value!r}")
    gnr, bnr, fnr, snr = parts
    return gnr, bnr, fnr, snr


def get_external_agreement_id(
    gnr: str, bnr: str, fnr: str, snr: str
) -> Optional[str]:
    """
    Call the API and return agreementId from the first item in the list, which
    will be used as ExternalAgreementId in SQL.
    Expected JSON format:
    [
        {
            "agreementId": "5775",
            "municipalityNumber": "1103",
            ...
        }
    ]
    """
    params = {"gnr": gnr, "bnr": bnr, "fnr": fnr, "snr": snr}

    resp = requests.get(API_BASE_URL, headers=HEADERS, params=params, timeout=20)
    resp.raise_for_status()

    sleep(0.01)

    data = resp.json()

    if not isinstance(data, list) or not data:
        return None

    first: Dict[str, Any] = data[0]
    agreement_id = first.get("agreementId")

    if agreement_id is None:
        return None

    # Ensure it's a string
    return str(agreement_id)


def sql_escape(value: str) -> str:
    """
    Escape single quotes for SQL string literals.
    """
    return value.replace("'", "''")


# --- MAIN LOGIC ---


def generate_update_statements_from_csv(csv_path: str) -> List[str]:
    sql_lines: List[str] = []

    # Use utf-8-sig to handle BOM, and normalize header keys
    with open(csv_path, newline="", encoding="utf-8-sig") as f:
        reader = csv.DictReader(f)

        for raw_row in reader:
            # Strip BOM from any header, just to be safe
            row = {k.lstrip("\ufeff"): v for k, v in raw_row.items()}

            gpsls_customer_id = row.get("GPSLSCustomerId")
            pa_system = row.get("PASystem")
            agreement_id = row.get("AgreementId")
            gnr_bnr_fnr_snr = row.get("GnrBnrFnrSnr")

            if not (gpsls_customer_id and pa_system and agreement_id and gnr_bnr_fnr_snr):
                print(f"Skipping row with missing required fields: {row}")
                continue

            try:
                gnr, bnr, fnr, snr = parse_gnr_bnr_fnr_snr(gnr_bnr_fnr_snr)
            except ValueError as ex:
                print(f"Error parsing GnrBnrFnrSnr for row {row}: {ex}")
                continue

            try:
                external_agreement_id = get_external_agreement_id(gnr, bnr, fnr, snr)
            except Exception as ex:
                print(
                    f"API error for GnrBnrFnrSnr={gnr_bnr_fnr_snr} "
                    f"(gnr={gnr}, bnr={bnr}, fnr={fnr}, snr={snr}): {ex}"
                )
                continue

            if not external_agreement_id:
                print(
                    f"No agreementId returned for "
                    f"GnrBnrFnrSnr={gnr_bnr_fnr_snr} (gnr={gnr}, bnr={bnr}, fnr={fnr}, snr={snr})"
                )
                continue

            ext_escaped = sql_escape(external_agreement_id)

            # Build the SQL line:
            # ExternalAgreementId gets the agreementId from API
            sql = (
                f"UPDATE {SQL_TABLE_NAME} "
                f"SET ExternalAgreementId = '{ext_escaped}' "
                f"WHERE GPSLSCustomerId = {gpsls_customer_id} "
                f"AND PASystem = '{sql_escape(pa_system)}' "
                f"AND AgreementId = {agreement_id};"
            )

            print(sql)

            sql_lines.append(sql)

    return sql_lines


def main():
    sql_lines = generate_update_statements_from_csv(CSV_PATH)

    if not sql_lines:
        print("No UPDATE statements were generated.")
        return

    # Print each line to console
    for line in sql_lines:
        print(line)

    # Save to file
    output_path = Path(OUTPUT_SQL_PATH)
    output_path.parent.mkdir(parents=True, exist_ok=True)

    with open(output_path, "w", encoding="utf-8") as f:
        for line in sql_lines:
            f.write(line + "\n")

    print(f"\nSaved {len(sql_lines)} UPDATE statements to: {output_path}")


if __name__ == "__main__":
    main()