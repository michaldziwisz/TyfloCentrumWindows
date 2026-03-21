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
- artefakt gotowy do submission
- dla lokalnych testow developerskich:
  - nie uzywac luznego `exe` z `bin/`
  - uzywac `Install.ps1` lub `Add-AppDevPackage.ps1` z wygenerowanego katalogu `AppPackages`

## Submission checklist
- opis aplikacji
- screenshoty
- polityka prywatnosci
- informacje o dostepnosci
- notatki testowe
- lista wspieranych architektur

## Ryzyka release
- push notifications moga wplynac na szczegoly konfiguracji pakietu
- mikrofon i audio wymagaja poprawnych capabilities i testow na maszynie docelowej
- nie nalezy odkladac pracy nad `MSIX` na koniec projektu

## Zasady projektowe pod release
- nie wprowadzac zaleznosci, ktore lamia model packaged app bez osobnej decyzji architektonicznej
- kazda decyzja o helper process lub dodatkowym executable wymaga rewizji modelu pakowania
- test instalacji, aktualizacji i odinstalowania jest obowiazkowy przed wydaniem

## Uwagi organizacyjne
- jesli produkt ma byc publikowany tylko przez Store, nalezy utrzymac proces release zgodny z tym scenariuszem od pierwszej wersji RC
- jesli w przyszlosci pojawi sie dystrybucja poza Store, trzeba dopisac osobna procedure i zweryfikowac wymagania podpisu

## Dokumenty powiazane
- [Architektura](architecture.md)
- [Roadmapa implementacji](implementation-roadmap.md)
- [ADR 0001](adr/0001-winui3-windowsappsdk-msix.md)
