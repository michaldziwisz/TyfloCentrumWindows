# Pakowanie MSIX i Microsoft Store

## Cel
Od poczatku projektu utrzymac model dystrybucji zgodny z Microsoft Store, tak aby proces publikacji nie wymagal przebudowy architektury na koncu wdrozenia.

## Docelowy model
- aplikacja packaged desktop app
- artefakt release: `MSIX`
- glowny kanal dystrybucji: Microsoft Store

## Zalozenia
- projekt powinien od poczatku uzywac modelu single-project `MSIX`
- release musi wspierac co najmniej `x64`
- `arm64` powinno byc przewidziane architektonicznie juz od poczatku

## Obszary do przygotowania

### Tozsamosc aplikacji
- nazwa i identyfikator produktu
- publisher identity po skojarzeniu z wpisem Store
- ikony i assety

### Manifest i capabilities
Do weryfikacji przed implementacja:
- siec
- mikrofon
- ewentualne powiadomienia
- ewentualne deklaracje zwiazane z media playback

### Pipeline release
- restore
- build
- test
- package `MSIX`
- package symbols `.appxsym`
- artefakt gotowy do submission
- dla lokalnych testow developerskich:
  - nie uzywac luznego `exe` z `bin/`
  - uzywac `Install.ps1` lub `Add-AppDevPackage.ps1` z wygenerowanego katalogu `AppPackages`

## Automatyzacja przez GitHub Actions
- repo ma przygotowane workflow do draft submission, release submission oraz metadata submission
- workflow opieraja sie o `Microsoft Store Developer CLI (preview)` i `microsoft/microsoft-store-apppublisher@v1.1`
- workflow sa uruchamiane recznie przez `workflow_dispatch`
- do automatycznej publikacji wymagane sa sekrety GitHub i `STORE_PRODUCT_ID`
- pierwsza konfiguracja aplikacji i pierwsza submission nadal musza byc wykonane recznie w Partner Center

Szczegoly:
- [Pierwsza publikacja w Partner Center](store-partner-center-checklist.md)
- [Microsoft Store przez GitHub Actions](store-publishing-github-actions.md)

## Submission checklist
- opis aplikacji
- screenshoty
- polityka prywatnosci
- informacje o dostepnosci
- notatki testowe
- lista wspieranych architektur
- numer wersji zgodny z Partner Center:
  - czwarty segment wersji musi pozostac `0`
  - przyklad poprawnej wersji: `0.1.4.0`
- dla kolejnych recznych update'ow warto przygotowac osobny plik z gotowym listingiem i `What's new`, np.:
  - [submission-0.1.4.0-pl-PL.md](../store/submission-0.1.4.0-pl-PL.md)

## Ryzyka release
- push notifications moga wplynac na szczegoly konfiguracji pakietu
- mikrofon i audio wymagaja poprawnych capabilities i testow na maszynie docelowej
- nie nalezy odkladac pracy nad `MSIX` na koniec projektu
- powiadomienia przy zamknietej aplikacji wymagaja poza kodem rowniez:
  - konfiguracji Azure App Registration
  - mapowania `PFN -> App ID`
  - wdrozonego nadawcy WNS po stronie `TyfloCentrum.PushService`

## Zasady projektowe pod release
- nie wprowadzac zaleznosci, ktore lamia model packaged app bez osobnej decyzji architektonicznej
- kazda decyzja o helper process lub dodatkowym executable wymaga rewizji modelu pakowania
- test instalacji, aktualizacji i odinstalowania jest obowiazkowy przed wydaniem

## Uwagi organizacyjne
- jesli produkt ma byc publikowany tylko przez Store, nalezy utrzymac proces release zgodny z tym scenariuszem od pierwszej wersji RC
- repo ma juz tez osobna procedure dla kanalu poza Store:
  - [Dystrybucja poza Store jako direct EXE](release-direct-exe.md)
- kanal direct EXE nie zastępuje Store i nie daje podpisanego `MSIX/AppInstaller`; to osobna sciezka dystrybucji poza ekosystemem Store

## Dokumenty powiazane
- [Architektura](architecture.md)
- [Roadmapa implementacji](implementation-roadmap.md)
- [ADR 0001](adr/0001-winui3-windowsappsdk-msix.md)
- [Push Service dla Windows](push-service-windows.md)
- [Dystrybucja poza Store jako direct EXE](release-direct-exe.md)
