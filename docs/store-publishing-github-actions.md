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

Pelna checklista pierwszej publikacji:
- [Pierwsza publikacja w Partner Center](store-partner-center-checklist.md)

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

Stan repo `TyfloCentrumWindows` po pierwszej publikacji:
- `STORE_PRODUCT_ID = 9N62MNLNN9J6` jest juz ustawione jako repository variable
- nadal trzeba jeszcze ustawic sekrety:
  - `AZURE_AD_APPLICATION_CLIENT_ID`
  - `AZURE_AD_APPLICATION_SECRET`
  - `AZURE_AD_TENANT_ID`
  - `SELLER_ID`

## Dostepne workflow

### `Store Draft Submission`
Plik:
- `.github/workflows/store-draft.yml`

Przeznaczenie:
- buduje release `MSIX`
- generuje `.appxsym`
- generuje tez `.msixupload`
- wysyla pakiet do Microsoft Store jako draft
- nie commit-uje submission
- eksportuje aktualny draft metadata jako artefakt workflow
- pozwala wskazac `git_ref`, wiec mozna zbudowac draft z `main`, tagu albo konkretnego commita

### `Store Release Submission`
Plik:
- `.github/workflows/store-release.yml`

Przeznaczenie:
- buduje release `MSIX`
- generuje `.appxsym`
- generuje tez `.msixupload`
- wysyla submission do Store
- opcjonalnie obsluguje rollout procentowy
- czeka na finalny status submission
- pozwala wskazac `git_ref`, wiec aktualizacja moze byc wydana z tagu release albo brancha

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
1. Wprowadz zmiany do aplikacji.
2. Podnies `Version` w `src/TyfloCentrum.Windows.App/Package.appxmanifest`.
3. Wypchnij commit i opcjonalnie utworz tag release.
4. Uruchom `Store Draft Submission` z odpowiednim `git_ref`.
5. Zweryfikuj draft w Partner Center.
6. Jesli trzeba zmienic listingi, uruchom `Store Get Base Metadata`, zaktualizuj JSON i uruchom `Store Update Metadata`.
7. Uruchom `Store Release Submission` z tym samym `git_ref`.

Najpraktyczniejszy `git_ref` przy kolejnych wydaniach:
- `main`, jesli wydajesz bez tagowania
- tag, np. `v0.1.1`, jesli chcesz miec w Store dokladnie zmapowane wydanie do commita

## Artefakty workflow
Workflow pakietujace generuja:
- `.msixupload`
- `.msix`
- `.appxsym`

To oznacza, ze przy publikacji masz od razu paczke symboli do diagnostyki crashy i problemow z wydaniem.

## Uwagi praktyczne
- workflow sa uruchamiane recznie przez `workflow_dispatch`
- nie sa podpiete pod `push`, zeby uniknac przypadkowej publikacji
- do lokalnych testow nadal uzywaj `Build-DevMsix.ps1 -Install`
- do Store idzie build `Release`
- Store nie przyjmie aktualizacji, jesli numer `Version` w `Package.appxmanifest` nie bedzie wyzszy niz w poprzednim wydaniu

## Powiazane dokumenty
- [Pierwsza publikacja w Partner Center](store-partner-center-checklist.md)
- [Pakowanie MSIX i Microsoft Store](release-msix-store.md)
- [Setup deweloperski](developer-setup.md)
