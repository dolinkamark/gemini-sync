import csv
import json
from datetime import datetime

input_csv = r"E:\Temp\Ymir\logline_export.txt"
output_json = r"E:\Temp\Ymir\logline_export.json"

def to_zulu_format(date_str: str) -> str:
    """
    Convert 'YYYY-MM-DD HH:MM:SS.fff' to 'YYYY-MM-DDTHH:MM:SSZ'
    without changing the actual time value.
    """
    dt = datetime.strptime(date_str, "%Y-%m-%d %H:%M:%S.%f")
    return dt.strftime("%Y-%m-%dT%H:%M:%SZ")

data = []

with open(input_csv, newline='', encoding='utf-8') as csvfile:
    reader = csv.DictReader(csvfile)

    for row in reader:
        # Convert numeric fields
        row["LogLineId"] = int(row["LogLineId"])
        row["AgreementLineId"] = int(row["AgreementLineId"])
        row["PlaceNr"] = int(row["PlaceNr"])
        row["UnitId"] = int(row["UnitId"])

        # Convert time to Zulu format
        if row.get("Time"):
            row["Time"] = to_zulu_format(row["Time"])

        data.append(row)

with open(output_json, "w", encoding="utf-8") as jsonfile:
    json.dump(data, jsonfile, indent=4, ensure_ascii=False)

print(f"JSON written to {output_json}")
