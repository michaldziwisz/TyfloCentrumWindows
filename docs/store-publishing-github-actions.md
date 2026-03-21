# Microsoft Store przez GitHub Actions

## Cel
Zautomatyzowac publikacje `TyfloCentrum` do Microsoft Store przez GitHub Actions, z zachowaniem bezpiecznego modelu:
- osobny workflow do draftu
- osobny workflow do publikacji release
- osobne workflow do metadata submission

## Stan na 2026-03-21
Automatyzacja opiera sie o oficjalny:
- `Microsoft Store Developer CLI (preview)`
- GitHub Action `microsoft/microsoft-store-apppublisher@v1.1`

Aktualne ograniczenie z dokumentacji Microsoft:
- GitHub Actions dla aktualizacji aplikacji wspiera obecnie tylko produkty bezplatne

## Jednorazowy setup poza repo
Przed pierwsza automatyczna publikacja musisz recznie:
1. zarezerwowac i skonfigurowac aplikacje w Partner Center
2. wykonac pierwsza submission w Partner Center
3. skojarzyc konto Partner Center z Microsoft Entra ID
4. zarejestrowac aplikacje Entra ID do API
5. nadac tej aplikacji role `Manager` w Partner Center

## Konfiguracja GitHub
W repo trzeba ustawic:

### Repository secrets
- `AZURE_AD_APPLICATION_CLIENT_ID`
- `AZURE_AD_APPLICATION_SECRET`
- `AZURE_AD_TENANT_ID`
- `SELLER_ID`

### Repository variables
- `STORE_PRODUCT_ID`

`STORE_PRODUCT_ID` nie jest sekretem, ale bez niego workflow nie bedzie wiedzial, do ktorej aplikacji w Store wysylac submission.

## Dostepne workflow

### `Store Draft Submission`
Plik:
- `.github/workflows/store-draft.yml`

Przeznaczenie:
- buduje release `MSIX`
- generuje `.appxsym`
- wysyla pakiet do Microsoft Store jako draft
- nie commit-uje submission
- eksportuje aktualny draft metadata jako artefakt workflow

### `Store Release Submission`
Plik:
- `.github/workflows/store-release.yml`

Przeznaczenie:
- buduje release `MSIX`
- generuje `.appxsym`
- wysyla submission do Store
- opcjonalnie obsluguje rollout procentowy
- czeka na finalny status submission

### `Store Get Base Metadata`
Plik:
- `.github/workflows/store-get-base-metadata.yml`

Przeznaczenie:
- pobiera aktualne metadata submission z Partner Center
- zapisuje je jako artefakt `store-base-metadata`

### `Store Update Metadata`
Plik:
- `.github/workflows/store-update-metadata.yml`

Przeznaczenie:
- aktualizuje metadata draftu na podstawie pliku JSON z repo
- opcjonalnie publikuje metadata submission od razu po aktualizacji

## Rekomendowany flow publikacji
1. W Partner Center utworz i skonfiguruj aplikacje recznie.
2. Ustaw sekrety i zmienna `STORE_PRODUCT_ID` w GitHub.
3. Uruchom `Store Get Base Metadata`.
4. Pobierz JSON i zapisz go jako `store/metadata/submission.json`.
5. Edytuj metadata w repo.
6. Uruchom `Store Update Metadata`.
7. Uruchom `Store Draft Submission`.
8. Zweryfikuj draft w Partner Center.
9. Uruchom `Store Release Submission`.

## Artefakty workflow
Workflow pakietujace generuja:
- `.msix`
- `.appxsym`

To oznacza, ze przy publikacji masz od razu paczke symboli do diagnostyki crashy i problemow z wydaniem.

## Uwagi praktyczne
- workflow sa uruchamiane recznie przez `workflow_dispatch`
- nie sa podpiete pod `push`, zeby uniknac przypadkowej publikacji
- do lokalnych testow nadal uzywaj `Build-DevMsix.ps1 -Install`
- do Store idzie build `Release`

## Powiazane dokumenty
- [Pakowanie MSIX i Microsoft Store](release-msix-store.md)
- [Setup deweloperski](developer-setup.md)
