#!/usr/bin/env python3
"""
Fetch agreement search results from Gemini Test and Prod for configured
matrikkel ids (gnr.bnr.fnr.snr), then write a single JSON file of
MatrikkelId/AgreementId pairs that exist in only one environment.
"""
from __future__ import annotations

import json
import ssl
from datetime import datetime
from pathlib import Path
from typing import Any, Dict, Optional, Set, Tuple
from urllib.parse import urlencode

import requests
from requests.adapters import HTTPAdapter

# ========= CONFIG (edit these) =========
MATRIKKEL_IDS = [
    "1.6.0.0", "111.4.0.0", "112.20.0.0", "118.16.0.0", "125.16.0.0", "125.25.0.0", "141.91.0.0", "143.36.0.0",
    "154.7.0.0", "155.2.0.0", "157.19.0.0", "16.1459.0.40", "16.1745.0.0", "16.4.0.0", "16.578.0.0", "164.88.0.0",
    "169.2.0.0", "17.2456.0.0", "17.2461.0.0", "17.2465.0.0", "17.2923.0.0", "17.367.0.0", "17.860.0.0", "18.42.0.0",
    "182.28.0.0", "186.56.0.0", "223.22.0.0", "233.25.0.0", "235.135.0.0", "235.23.0.0", "236.28.0.0", "236.29.0.0",
    "239.29.0.0", "242.4.0.0", "244.49.0.0", "244.78.0.0", "246.25.0.0", "246.31.0.0", "246.63.0.0", "249.2.0.0",
    "26.103.0.28", "26.103.0.29", "26.103.0.30", "26.103.0.31", "26.103.0.32", "26.103.0.33", "26.103.0.34",
    "26.103.0.35", "26.103.0.36", "26.103.0.37", "26.103.0.38", "26.103.0.39", "26.304.0.30", "28.181.0.2",
    "28.2106.0.0", "28.3880.0.0", "29.169.0.0", "29.78.0.0", "3.1.0.0", "304.40.0.0", "31.1.0.0", "38.1283.0.1",
    "38.38.0.0", "38.4165.0.0", "38.814.0.0", "39.1.0.0", "39.173.0.0", "39.333.0.0", "4.164.0.0", "40.310.0.2",
    "41.1548.0.2", "41.1548.0.9", "41.1569.0.0", "41.172.0.0", "41.889.0.0", "52.172.0.1", "52.172.0.2", "52.172.0.3",
    "52.172.0.4", "52.172.0.5", "52.172.0.6", "52.271.0.2", "52.272.0.2", "53.173.0.1", "53.173.0.2", "55.249.0.0", "55.5.0.4",
    "56.1757.0.2", "56.605.0.2", "58.856.0.6", "58.856.0.9", "58.870.0.2", "59.1365.0.1", "59.543.0.2", "59.950.0.2",
    "6.33.0.0", "7.1255.0.5"
]
OUTPUT_DIR = r"E:\Temp\Ymir_Matrikkel_Diffs"
TIMEOUT_SECONDS = 30

# ======================================

SEARCH_PATH = "/public/invoicing/api/agreements/search"

APIS: Dict[str, Dict[str, str]] = {
    "test": {
        "name": "Test",
        "host": "https://powelqapfpublicapi.azure-api.net",
        "municipality_no": "stavangerkundetest",
        "subscription_key": "3d8d028ee9be4cc9a9e4ac0a92068966",
    },
    "prod": {
        "name": "Prod",
        "host": "https://pfpublicapi.geminisuite.com",
        "municipality_no": "stavanger",
        "subscription_key": "f714fceb470744ffa6017cfb050ffcbb",
    },
}


def _system_ssl_context() -> ssl.SSLContext:
    """Build a TLS context that uses the OS certificate store.

    requests defaults to certifi, which does not include local CAs injected by
    antivirus HTTPS scanning (for example Avast Web Shield). Python 3.13 also
    enables OpenSSL X509_STRICT, which rejects some of those CAs because Basic
    Constraints is not marked critical. Hostname and certificate verification
    stay enabled.
    """
    context = ssl.create_default_context()
    if hasattr(ssl, "VERIFY_X509_STRICT"):
        context.verify_flags &= ~ssl.VERIFY_X509_STRICT
    return context


class SystemCertAdapter(HTTPAdapter):
    def init_poolmanager(self, *args, **kwargs):
        kwargs["ssl_context"] = _system_ssl_context()
        return super().init_poolmanager(*args, **kwargs)

    def proxy_manager_for(self, *args, **kwargs):
        kwargs["ssl_context"] = _system_ssl_context()
        return super().proxy_manager_for(*args, **kwargs)


def http_session() -> requests.Session:
    session = requests.Session()
    adapter = SystemCertAdapter()
    session.mount("https://", adapter)
    return session


_SESSION = http_session()


def parse_matrikkel(value: str) -> Tuple[str, str, str, str]:
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


def build_headers(api: Dict[str, str]) -> Dict[str, str]:
    """
    Build request headers for a Gemini public API.

    Azure APIM requires ocp-apim-subscription-key. Municipality routing uses
    municipalityNo. Only add a header when the config value is non-empty so
    Test (stavangerkundetest) and Prod (stavanger) can differ safely.
    """
    headers: Dict[str, str] = {
        "Accept": "application/json",
    }

    municipality_no = (api.get("municipality_no") or "").strip()
    if municipality_no:
        headers["municipalityNo"] = municipality_no

    subscription_key = (api.get("subscription_key") or "").strip()
    if subscription_key:
        headers["ocp-apim-subscription-key"] = subscription_key

    return headers


def build_search_url(api: Dict[str, str], gnr: str, bnr: str, fnr: str, snr: str) -> str:
    query = urlencode({"gnr": gnr, "bnr": bnr, "fnr": fnr, "snr": snr})
    return f"{api['host'].rstrip('/')}{SEARCH_PATH}?{query}"


def fetch_agreements(
    api: Dict[str, str], gnr: str, bnr: str, fnr: str, snr: str
) -> Any:
    url = build_search_url(api, gnr, bnr, fnr, snr)
    headers = build_headers(api)
    response = _SESSION.get(url, headers=headers, timeout=TIMEOUT_SECONDS)
    response.raise_for_status()
    return response.json()


def agreement_ids_from_payload(payload: Any) -> Set[str]:
    if not isinstance(payload, list):
        return set()
    ids: Set[str] = set()
    for item in payload:
        if not isinstance(item, dict):
            continue
        agreement_id = item.get("agreementId")
        if agreement_id is not None and str(agreement_id).strip():
            ids.add(str(agreement_id))
    return ids


def fetch_agreement_ids(
    api: Dict[str, str], gnr: str, bnr: str, fnr: str, snr: str
) -> Optional[Set[str]]:
    try:
        payload = fetch_agreements(api, gnr, bnr, fnr, snr)
    except requests.RequestException as ex:
        print(f"{api['name']} ERROR for {gnr}.{bnr}.{fnr}.{snr}: {ex}")
        return None
    return agreement_ids_from_payload(payload)


def pair_records(pairs: Set[Tuple[str, str]]) -> list[dict[str, str]]:
    return [
        {"MatrikkelId": matrikkel_id, "AgreementId": agreement_id}
        for matrikkel_id, agreement_id in sorted(pairs)
    ]


def main() -> None:
    output_dir = Path(OUTPUT_DIR)
    output_dir.mkdir(parents=True, exist_ok=True)

    only_in_test: Set[Tuple[str, str]] = set()
    only_in_prod: Set[Tuple[str, str]] = set()

    for raw_matrikkel in MATRIKKEL_IDS:
        gnr, bnr, fnr, snr = parse_matrikkel(raw_matrikkel)
        matrikkel = f"{gnr}.{bnr}.{fnr}.{snr}"

        test_ids = fetch_agreement_ids(APIS["test"], gnr, bnr, fnr, snr)
        prod_ids = fetch_agreement_ids(APIS["prod"], gnr, bnr, fnr, snr)

        if test_ids is None or prod_ids is None:
            print(f"{matrikkel}: skipped (lookup failed)")
            continue

        test_only = test_ids - prod_ids
        prod_only = prod_ids - test_ids
        only_in_test.update((matrikkel, agreement_id) for agreement_id in test_only)
        only_in_prod.update((matrikkel, agreement_id) for agreement_id in prod_only)

        if test_only or prod_only:
            print(
                f"{matrikkel}: only in Test={sorted(test_only)} "
                f"only in Prod={sorted(prod_only)}"
            )
        else:
            print(f"{matrikkel}: same AgreementIds")

    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_path = output_dir / f"matrikkel_diffs_{timestamp}.json"
    output_path.write_text(
        json.dumps(
            {
                "OnlyInTest": pair_records(only_in_test),
                "OnlyInProd": pair_records(only_in_prod),
            },
            indent=4,
            ensure_ascii=False,
        )
        + "\n",
        encoding="utf-8",
    )
    print(f"Saved: {output_path}")


if __name__ == "__main__":
    main()
