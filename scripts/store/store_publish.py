#!/usr/bin/env python3
"""
Pelna publikacja pakietu do Microsoft Store przez Store submission REST API,
z pominieciem `msstore` (ktory bywa zawodny: mylace 504, oraz NIE aktualizuje
listy applicationPackages przy --noCommit, przez co w submisji zostaje stary
pakiet z poprzedniego wydania).

Co robi:
  1. Loguje sie (OAuth client_credentials, resource=manage.devcenter).
  2. Znajduje/przygotowuje oczekujaca (pending) submisje:
     - jesli jest pending -> uzywa jej,
     - jesli nie -> POST tworzy nowa (dziedziczy dane z ostatniej opublikowanej).
  3. W danych submisji:
     - oznacza WSZYSTKIE istniejace applicationPackages jako 'PendingDelete',
     - dodaje nowy wpis pakietu {fileName, fileStatus:'PendingUpload'},
     - opcjonalnie aktualizuje listing (description/releaseNotes) z plikow
       store/listing/*.txt,
     - opcjonalnie ustawia rollout (ROLLOUT_PERCENTAGE).
  4. PUT zapisuje zmienione dane submisji.
  5. Pakuje .msixupload do ZIP (nazwa wpisu w ZIP == fileName) i wysyla na
     fileUploadUrl (Azure Blob SAS, PUT block blob).
  6. (--commit) POST /commit i poll statusu. Bez --commit zostaje draft do
     weryfikacji w Partner Center.

Wszystkie wywolania devcenter maja retry na przejsciowe HTTP 5xx/504.

Wymagane env: AZURE_AD_TENANT_ID, AZURE_AD_APPLICATION_CLIENT_ID,
  AZURE_AD_APPLICATION_SECRET, STORE_PRODUCT_ID
Opcjonalne env: ROLLOUT_PERCENTAGE (0-100)

Uzycie:
  python scripts/store/store_publish.py --package <sciezka.msixupload>
  python scripts/store/store_publish.py --package <...> --commit
  python scripts/store/store_publish.py --package <...> --dry-run
"""
import argparse
import io
import json
import os
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
import zipfile
from pathlib import Path

TOKEN_URL = "https://login.microsoftonline.com/{tenant}/oauth2/token"
RESOURCE = "https://manage.devcenter.microsoft.com"
API = "https://manage.devcenter.microsoft.com/v1.0/my/applications"
REPO_ROOT = Path(__file__).resolve().parents[2]
LISTING_DIR = REPO_ROOT / "store" / "listing"


def _req(url, method="GET", token=None, body=None, max_attempts=6, timeout=120):
    data = None
    headers = {}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    if body is not None:
        data = json.dumps(body).encode("utf-8")
        headers["Content-Type"] = "application/json"
    elif method in ("POST", "PUT"):
        # POST/PUT bez ciala (np. /commit) - devcenter zwraca 411 Length Required
        # bez jawnego Content-Length: 0. Wysylamy puste cialo z dlugoscia 0.
        data = b""
        headers["Content-Length"] = "0"
    last_err = None
    for attempt in range(1, max_attempts + 1):
        try:
            r = urllib.request.Request(url, data=data, headers=headers, method=method)
            with urllib.request.urlopen(r, timeout=timeout) as resp:
                raw = resp.read().decode("utf-8")
                return json.loads(raw) if raw else {}
        except urllib.error.HTTPError as e:
            code = e.code
            detail = e.read().decode("utf-8", "replace")[:400]
            last_err = f"HTTP {code}: {detail}"
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
        "grant_type": "client_credentials", "client_id": client_id,
        "client_secret": client_secret, "resource": RESOURCE,
    }).encode()
    r = urllib.request.Request(TOKEN_URL.format(tenant=tenant), data=data,
        headers={"Content-Type": "application/x-www-form-urlencoded"}, method="POST")
    for attempt in range(1, 6):
        try:
            with urllib.request.urlopen(r, timeout=60) as resp:
                return json.loads(resp.read().decode("utf-8"))["access_token"]
        except Exception as e:  # noqa: BLE001
            if attempt == 5:
                raise
            print(f"  token proba {attempt} nieudana ({e}); ponawiam...", flush=True)
            time.sleep(5)


def upload_zip_to_sas(sas_url, zip_bytes, max_attempts=6):
    """PUT block blob na Azure SAS URI."""
    last_err = None
    for attempt in range(1, max_attempts + 1):
        try:
            r = urllib.request.Request(sas_url, data=zip_bytes, method="PUT",
                headers={"x-ms-blob-type": "BlockBlob",
                         "Content-Type": "application/zip",
                         "Content-Length": str(len(zip_bytes))})
            with urllib.request.urlopen(r, timeout=600) as resp:
                return resp.status
        except urllib.error.HTTPError as e:
            last_err = f"HTTP {e.code}: {e.read().decode('utf-8','replace')[:300]}"
        except (urllib.error.URLError, TimeoutError, ConnectionError) as e:
            last_err = f"{type(e).__name__}: {e}"
        if attempt < max_attempts:
            print(f"  upload proba {attempt} nieudana ({last_err}); ponawiam...", flush=True)
            time.sleep(min(30, 8 * attempt))
    raise RuntimeError(f"Upload na SAS nie powiodl sie po {max_attempts} probach: {last_err}")


def load_listing_files():
    out = {}
    if not LISTING_DIR.is_dir():
        return out
    for f in LISTING_DIR.iterdir():
        if not f.is_file() or not f.name.endswith(".txt"):
            continue
        parts = f.name[:-4].split(".")
        if len(parts) != 2:
            continue
        locale, field = parts
        field_map = {"description": "description", "whatsnew": "releaseNotes"}
        if field in field_map:
            out.setdefault(locale, {})[field_map[field]] = f.read_text(encoding="utf-8").strip()
    return out


def apply_listing(sub, wanted):
    listings = sub.get("listings", {})
    changed = False
    for locale, fields in wanted.items():
        key = next((k for k in listings if k.lower() == locale.lower()), None)
        if key is None:
            print(f"  UWAGA: locale {locale} brak w submisji - pomijam.")
            continue
        base = listings[key].setdefault("baseListing", {})
        for field, value in fields.items():
            if value and (base.get(field) or "").strip() != value.strip():
                base[field] = value
                changed = True
                print(f"  listing {locale}.{field}: zaktualizowano")
    return changed


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--package", required=True, help="Sciezka do .msixupload")
    ap.add_argument("--commit", action="store_true", help="Zloz submisje do certyfikacji.")
    ap.add_argument("--dry-run", action="store_true", help="Pokaz plan, nie zapisuj.")
    args = ap.parse_args()

    pkg = Path(args.package).resolve()
    if not pkg.is_file():
        print(f"BLAD: nie ma pliku pakietu: {pkg}", file=sys.stderr)
        return 2
    pkg_name = pkg.name

    tenant = os.environ["AZURE_AD_TENANT_ID"]
    client_id = os.environ["AZURE_AD_APPLICATION_CLIENT_ID"]
    client_secret = os.environ["AZURE_AD_APPLICATION_SECRET"]
    app_id = os.environ["STORE_PRODUCT_ID"]

    print("Loguje sie do Store API...")
    token = get_token(tenant, client_id, client_secret)

    print("Pobieram aplikacje...")
    app = _req(f"{API}/{app_id}", token=token)
    pending = app.get("pendingApplicationSubmission")
    if pending:
        sub_id = pending["id"]
        print(f"Uzywam istniejacej pending submisji: {sub_id}")
    else:
        print("Brak pending - tworze nowa submisje...")
        created = _req(f"{API}/{app_id}/submissions", method="POST", token=token)
        sub_id = created["id"]
        print(f"Utworzono submisje: {sub_id}")

    sub = _req(f"{API}/{app_id}/submissions/{sub_id}", token=token)
    file_upload_url = sub.get("fileUploadUrl")
    if not file_upload_url:
        print("BLAD: submisja nie ma fileUploadUrl.", file=sys.stderr)
        return 3

    # 1) Oznacz istniejace pakiety do usuniecia, dodaj nowy wpis.
    old = sub.get("applicationPackages", [])
    for p in old:
        p["fileStatus"] = "PendingDelete"
        print(f"  pakiet {p.get('version')} {p.get('architecture')} -> PendingDelete")
    sub["applicationPackages"] = old + [{"fileName": pkg_name, "fileStatus": "PendingUpload"}]
    print(f"  dodaje nowy pakiet: {pkg_name} (PendingUpload)")

    # 2) Listing z plikow.
    apply_listing(sub, load_listing_files())

    # 3) Rollout opcjonalnie.
    rollout_raw = (os.environ.get("ROLLOUT_PERCENTAGE") or "").strip()
    if rollout_raw:
        pct = float(rollout_raw)
        if not 0 <= pct <= 100:
            print(f"BLAD: ROLLOUT_PERCENTAGE={pct} poza 0-100.", file=sys.stderr)
            return 3
        sub.setdefault("packageDeliveryOptions", {})["packageRollout"] = {
            "isPackageRollout": True, "packageRolloutPercentage": pct}
        print(f"  packageRollout: {pct}%")

    if args.dry_run:
        print("--dry-run: nie zapisuje, nie wysylam, nie commituje.")
        return 0

    # 4) Zapisz zmienione dane submisji.
    print("Zapisuje dane submisji (PUT)...")
    _req(f"{API}/{app_id}/submissions/{sub_id}", method="PUT", token=token, body=sub)

    # 5) Spakuj pakiet do ZIP (nazwa wewnatrz == fileName) i wyslij na SAS.
    print("Pakuje pakiet do ZIP...")
    buf = io.BytesIO()
    with zipfile.ZipFile(buf, "w", zipfile.ZIP_DEFLATED) as z:
        z.write(pkg, arcname=pkg_name)
    zip_bytes = buf.getvalue()
    print(f"Wysylam ZIP na Azure Blob (SAS), {len(zip_bytes)} B...")
    status = upload_zip_to_sas(file_upload_url, zip_bytes)
    print(f"  upload HTTP {status}")

    if not args.commit:
        print("OK: pakiet i listing przygotowane w draftcie (bez commitu).")
        print("Zweryfikuj w Partner Center, potem uruchom z --commit lub kliknij Submit.")
        return 0

    # 6) Commit + poll.
    print("Skladam submisje (commit)...")
    _req(f"{API}/{app_id}/submissions/{sub_id}/commit", method="POST", token=token)
    print("Commit wyslany. Sprawdzam status...")
    for _ in range(20):
        st = _req(f"{API}/{app_id}/submissions/{sub_id}/status", token=token)
        status = st.get("status")
        print(f"  status: {status}")
        if status in ("CommitStarted", "PreProcessing", "Certification", "Release",
                      "Published", "PendingPublication"):
            print("OK: submisja przyjeta do przetwarzania/certyfikacji.")
            return 0
        if status and ("Failed" in status or "Invalid" in status):
            print("BLAD statusu:", json.dumps(st.get("statusDetails", {}), indent=2)[:800],
                  file=sys.stderr)
            return 4
        time.sleep(15)
    print("Commit wyslany; status jeszcze sie ustala - sprawdz w Partner Center.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
