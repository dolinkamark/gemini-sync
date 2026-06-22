import csv
import json
from datetime import datetime

input_csv = r"E:\Temp\Ymir\agreements_connections_271_20260511.csv"
output_json = r"E:\Temp\Ymir\agreements_connections_271_20260511.json"

def to_zulu_format(date_str: str) -> str:
    """
    Convert 'YYYY-MM-DD HH:MM:SS.fff' to 'YYYY-MM-DDTHH:MM:SSZ'
    without changing the actual time value.
    """
    dt = datetime.strptime(date_str, "%Y-%m-%d %H:%M:%S.%f")
    return dt.strftime("%Y-%m-%dT%H:%M:%SZ")

def convert_agreement_lines():
    data = []

    with open(input_csv, newline='', encoding='utf-8') as csvfile:
        reader = csv.DictReader(csvfile)

        for row in reader:
            # Convert numeric fields
            row["CustomerId"] = int(row["CustomerId"])
            row["AgreementLineId"] = int(row["AgreementLineId"])
            row["AgreementId"] = int(row["AgreementId"])
            row["PlaceNr"] = int(row["PlaceNr"])
            row["ShortName"] = int(row["ShortName"])

            row["HasLock"] = bool(row["HasLock"])

            if row.get("Termin") == "NULL":
                row["Termin"] = None

            # Convert time to Zulu format
            if row.get("RegDate"):
                row["RegDate"] = to_zulu_format(row["RegDate"])

            if row.get("FromDate"):
                row["FromDate"] = to_zulu_format(row["FromDate"])

            if row.get("ToDate"):
                row["ToDate"] = to_zulu_format(row["ToDate"])

            if row.get("LastChanged"):
                row["LastChanged"] = to_zulu_format(row["LastChanged"])

            data.append(row)

    with open(output_json, "w", encoding="utf-8") as jsonfile:
        json.dump(data, jsonfile, indent=4, ensure_ascii=False)

    print(f"JSON written to {output_json}")


def convert_agreement_places():
    data = []

    with open(input_csv, newline='', encoding='utf-8') as csvfile:
        reader = csv.DictReader(csvfile)

        for row in reader:
            if row["ExternalAgreementId"] == "NULL":
                continue

            # Convert numeric fields
            row["CustomerId"] = int(row["CustomerId"])
            row["ExternalAgreementId"] = int(row["ExternalAgreementId"])
            row["AgreementId"] = int(row["AgreementId"])
            row["PlaceNr"] = int(row["PlaceNr"])

            # Convert time to Zulu format
            if row.get("FromDate") == "NULL":
                row["FromDate"] = None
            else:
                row["FromDate"] = to_zulu_format(row["FromDate"])

            if row.get("ToDate") == "NULL":
                row["ToDate"] = None
            else:
                row["ToDate"] = to_zulu_format(row["ToDate"])

            if row.get("CreatedAt"):
                row["CreatedAt"] = to_zulu_format(row["CreatedAt"])

            if row.get("UpdatedAt") == "NULL":
                row["UpdatedAt"] = None
            else:
                row["UpdatedAt"] = to_zulu_format(row["UpdatedAt"])

            data.append(row)

    with open(output_json, "w", encoding="utf-8") as jsonfile:
        json.dump(data, jsonfile, indent=4, ensure_ascii=False)

    print(f"JSON written to {output_json}")



def convert_agreement_connection():
    data = []

    with open(input_csv, newline='', encoding='utf-8') as csvfile:
        reader = csv.DictReader(csvfile)

        for row in reader:
            if row["ExternalAgreementId"] == "NULL":
                continue

            # Convert numeric fields
            row["CustomerId"] = int(row["CustomerId"])
            row["ExternalAgreementId"] = int(row["ExternalAgreementId"])
            row["AgreementId"] = int(row["AgreementId"])
            row["PlaceNr"] = int(row["PlaceNr"])


            if row.get("PlaceTypeId") == "NULL":
                row["PlaceTypeId"] = None
            else:
                row["PlaceTypeId"] = int(row["PlaceTypeId"])

            # Convert time to Zulu format
            if row.get("Description") == "NULL":
                row["Description"] = None

            if row.get("FromDate") == "NULL":
                row["FromDate"] = None
            else:
                row["FromDate"] = to_zulu_format(row["FromDate"])

            if row.get("ToDate") == "NULL":
                row["ToDate"] = None
            else:
                row["ToDate"] = to_zulu_format(row["ToDate"])

            if row.get("CreatedAt"):
                row["CreatedAt"] = to_zulu_format(row["CreatedAt"])

            if row.get("UpdatedAt") == "NULL":
                row["UpdatedAt"] = None
            else:
                row["UpdatedAt"] = to_zulu_format(row["UpdatedAt"])

            data.append(row)


    with open(output_json, "w", encoding="utf-8") as jsonfile:
        json.dump(data, jsonfile, indent=4, ensure_ascii=False)

    print(f"JSON written to {output_json}")


if __name__ == "__main__":
    convert_agreement_connection()