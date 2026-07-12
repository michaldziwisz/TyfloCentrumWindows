#!/usr/bin/env python3
"""
Aktualizuje metadane (listing) OCZEKUJACEJ submisji w Microsoft Store przez
Store submission REST API. Uzywane po `msstore publish --noCommit`, ktore
wgrywa TYLKO pakiet i NIE rusza tekstow listingu.

Dlaczego REST, a nie `msstore submission updateMetadata`:
- API devcenter bywa niestabilne (przejsciowe HTTP 504); tu mamy wlasny retry.
- Pelna, jawna kontrola nad tym, ktore pola listingu nadpisujemy.

Zrodlo prawdy o tekstach = pliki w repo:
- store/listing/<locale>.description.txt  -> baseListing.description
- store/listing/<locale>.whatsnew.txt     -> baseListing.releaseNotes

Wymagane zmienne srodowiskowe (te same sekrety co workflow):
  AZURE_AD_TENANT_ID, AZURE_AD_APPLICATION_CLIENT_ID,
  AZURE_AD_APPLICATION_SECRET, STORE_PRODUCT_ID
Opcjonalnie:
  ROLLOUT_PERCENTAGE - procent rolloutu pakietu (0-100). Pusto = pelne wydanie.

Uzycie:
  python scripts/store/update_listing.py            # aktualizuje wszystkie locale z plikow
  python scripts/store/update_listing.py --locale pl-PL
  python scripts/store/update_listing.py --dry-run  # tylko pokaz co by zmienil
"""
import argparse
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

TOKEN_URL = "https://login.microsoftonline.com/{tenant}/oauth2/token"
RESOURCE = "https://manage.devcenter.microsoft.com"
API = "https://manage.devcenter.microsoft.com/v1.0/my/applications"

REPO_ROOT = Path(__file__).resolve().parents[2]
LISTING_DIR = REPO_ROOT / "store" / "listing"


def _req(url, method="GET", token=None, body=None, max_attempts=6, timeout=90):
    """HTTP z retry na 5xx/timeout (devcenter potrafi zwracac 504)."""
    data = None
    headers = {}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    last_err = None
    for attempt in range(1, max_attempts + 1):
        try:
            r = urllib.request.Request(url, data=data, headers=headers, method=method)
            with urllib.request.urlopen(r, timeout=timeout) as resp:
                raw = resp.read().decode("utf-8")
                return json.loads(raw) if raw else {}
        except urllib.error.HTTPError as e:
            code = e.code
            detail = e.read().decode("utf-8", "replace")[:300]
            last_err = f"HTTP {code}: {detail}"
            # 5xx = przejsciowe -> ponow; 4xx (poza 429) = trwale -> przerwij
            if code < 500 and code != 429:
                raise RuntimeError(f"{method} {url} -> {last_err}")
        except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
            last_err = f"{type(e).__name__}: {e}"
        if attempt < max_attempts:
            wait = min(60, 10 * attempt)
            print(f"  proba {attempt} nieudana ({last_err}); czekam {wait}s...", flush=True)
            time.sleep(wait)
    raise RuntimeError(f"{method} {url} nie powiodl sie po {max_attempts} probach: {last_err}")


def get_token(tenant, client_id, client_secret):
    data = urllib.parse.urlencode({
        "grant_type": "client_credentials",
        "client_id": client_id,
        "client_secret": client_secret,
        "resource": RESOURCE,
    }).encode()
    r = urllib.request.Request(
        TOKEN_URL.format(tenant=tenant), data=data,
        headers={"Content-Type": "application/x-www-form-urlencoded"}, method="POST",
    )
    for attempt in range(1, 6):
        try:
            with urllib.request.urlopen(r, timeout=60) as resp:
                return json.loads(resp.read().decode("utf-8"))["access_token"]
        except Exception as e:  # noqa: BLE001
            if attempt == 5:
                raise
            print(f"  token proba {attempt} nieudana ({e}); ponawiam...", flush=True)
            time.sleep(5)


def load_listing_files():
    """Zwraca {locale: {'description': str|None, 'releaseNotes': str|None}}."""
    out = {}
    if not LISTING_DIR.is_dir():
        return out
    for f in LISTING_DIR.iterdir():
        if not f.is_file() or not f.name.endswith(".txt"):
            continue
        # format: <locale>.<field>.txt np. pl-PL.description.txt
        parts = f.name[:-4].split(".")
        if len(parts) != 2:
            continue
        locale, field = parts
        field_map = {"description": "description", "whatsnew": "releaseNotes"}
        if field not in field_map:
            continue
        out.setdefault(locale, {})[field_map[field]] = f.read_text(encoding="utf-8").strip()
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--locale", help="Tylko ten locale (np. pl-PL). Domyslnie: wszystkie z plikow.")
    ap.add_argument("--dry-run", action="store_true", help="Pokaz zmiany, nie zapisuj.")
    args = ap.parse_args()

    tenant = os.environ["AZURE_AD_TENANT_ID"]
    client_id = os.environ["AZURE_AD_APPLICATION_CLIENT_ID"]
    client_secret = os.environ["AZURE_AD_APPLICATION_SECRET"]
    app_id = os.environ["STORE_PRODUCT_ID"]

    wanted = load_listing_files()
    if args.locale:
        wanted = {k: v for k, v in wanted.items() if k.lower() == args.locale.lower()}
    if not wanted:
        print("Brak plikow listingu do zastosowania (store/listing/*.txt).")
        return 1
    print("Locale do aktualizacji:", ", ".join(sorted(wanted)))

    print("Loguje sie do Store API...")
    token = get_token(tenant, client_id, client_secret)

    print("Pobieram aplikacje...")
    app = _req(f"{API}/{app_id}", token=token)
    pending = app.get("pendingApplicationSubmission")
    if not pending:
        print("BLAD: brak oczekujacej submisji (pendingApplicationSubmission). "
              "Najpierw wgraj pakiet (Store Draft), potem aktualizuj listing.", file=sys.stderr)
        return 2
    sub_id = pending["id"]
    print(f"Oczekujaca submisja: {sub_id}")

    print("Pobieram tresc submisji...")
    sub = _req(f"{API}/{app_id}/submissions/{sub_id}", token=token)
    listings = sub.get("listings", {})

    changed = False
    for locale, fields in wanted.items():
        # API klucze locale sa malymi literami (np. 'pl-pl')
        key = next((k for k in listings if k.lower() == locale.lower()), None)
        if key is None:
            print(f"  UWAGA: locale {locale} nie istnieje w submisji - pomijam "
                  f"(dostepne: {', '.join(sorted(listings)) or 'brak'}).")
            continue
        base = listings[key].setdefault("baseListing", {})
        for field, value in fields.items():
            if value is None:
                continue
            old = (base.get(field) or "").strip()
            if old == value.strip():
                print(f"  {locale}.{field}: bez zmian")
                continue
            base[field] = value
            changed = True
            print(f"  {locale}.{field}: ZAKTUALIZOWANO ({len(value)} znakow)")

    # Opcjonalny rollout procentowy (tylko release). Pusto/brak = pelne wydanie.
    rollout_raw = (os.environ.get("ROLLOUT_PERCENTAGE") or "").strip()
    if rollout_raw:
        try:
            pct = float(rollout_raw)
        except ValueError:
            print(f"BLAD: ROLLOUT_PERCENTAGE='{rollout_raw}' nie jest liczba.", file=sys.stderr)
            return 3
        if not 0 <= pct <= 100:
            print(f"BLAD: ROLLOUT_PERCENTAGE={pct} poza zakresem 0-100.", file=sys.stderr)
            return 3
        pdo = sub.setdefault("packageDeliveryOptions", {})
        pdo["packageRollout"] = {
            "isPackageRollout": True,
            "packageRolloutPercentage": pct,
        }
        changed = True
        print(f"  packageRollout: {pct}%")

    if not changed:
        print("Nic do zapisania - listing juz aktualny.")
        return 0
    if args.dry_run:
        print("--dry-run: nie zapisuje.")
        return 0

    print("Zapisuje zmieniony listing (PUT submission)...")
    _req(f"{API}/{app_id}/submissions/{sub_id}", method="PUT", token=token, body=sub)
    print("OK: listing zaktualizowany w oczekujacej submisji.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
