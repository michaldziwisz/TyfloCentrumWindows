# TyfloCentrum Windows

Desktopowa wersja aplikacji TyfloCentrum dla systemu Windows, projektowana od początku z priorytetem na dostepnosc dla czytnikow ekranu i dystrybucje przez Microsoft Store.

Status repo:
- foundation aplikacji i shell `WinUI 3` są gotowe
- dokumentacja startowa jest gotowa
- działają już sekcje: `Nowości`, `Podcasty`, `Artykuły`, `Szukaj`, `Ulubione`, `Tyfloradio`
- listy `Nowości`, `Podcasty`, `Artykuły` i `Szukaj` mają domyślne akcje na `Enter` i menu kontekstowe dla akcji pobocznych zamiast osobnych przycisków w każdym wierszu
- listy `Nowości`, `Podcasty` i `Artykuły` dociągają automatycznie starsze treści podczas przewijania
- działa już pierwszy wariant szczegółów wpisu, komentarzy podcastów z licznikiem i szczegółami komentarza oraz wbudowanego odtwarzacza audio
- działa już pierwsza wersja wewnętrznej przeglądarki artykułów opartej o `WebView2`, w widoku czytania opartym o treść z API
- działają już skróty klawiaturowe dla głównych sekcji aplikacji i podstawowych akcji odtwarzacza
- player zapamiętuje pozycję podcastu i wznawia od ostatniego miejsca po ponownym otwarciu
- działa już pierwszy wariant dodatków playera: znaczniki czasu i odnośniki wyciągane z komentarzy podcastu, z kopiowaniem linków
- działa już rozszerzona wersja ulubionych z lokalnym JSON-em, filtrami `Podcasty/Artykuły/Tematy/Linki`, obsługą stron `TyfloŚwiata`, tematów odcinka i odnośników oraz kopiowaniem i udostępnianiem linków
- działa już formularz kontaktu tekstowego do Tyfloradia
- działa już pierwsza wersja głosówek z nagrywaniem `RAW`, appendem fragmentów, trybem `przytrzymaj i mów`, systemowym promptem zgody na mikrofon przy pierwszej próbie, komunikatami przerwań, odsłuchem i uploadem
- działa już sekcja `Ustawienia` z wyborem urządzenia wejściowego i wyjściowego, domyślną prędkością odtwarzania i zapamiętywaniem ostatniej prędkości
- działa już pierwszy wariant `TyfloŚwiata`: roczniki, numery czasopisma, PDF, spis treści artykułów z `pages`, szybkie dodawanie stron do ulubionych i udostępnianie linków
- docelowy stack: `WinUI 3`, `Windows App SDK`, `.NET 8`, `MSIX`

## Cele produktu
- odtworzyc funkcjonalnosc obecnej aplikacji iOS `Tyflocentrum`
- zapewnic wysoka jakosc obslugi `Narrator` i `NVDA`
- dostarczyc pakiet `MSIX` gotowy do publikacji w Microsoft Store

## Najwazniejsze dokumenty
- [Mapa dokumentacji](docs/index.md)
- [Plan i backlog wdrozenia](docs/windows-plan-and-backlog.md)
- [Wymagania produktowe](docs/product-requirements.md)
- [Architektura](docs/architecture.md)
- [Wymagania dostepnosci](docs/accessibility-requirements.md)
- [Integracje API](docs/api-integrations.md)
- [Strategia testow](docs/testing-strategy.md)
- [Setup deweloperski](docs/developer-setup.md)
- [Release do MSIX i Microsoft Store](docs/release-msix-store.md)

## Repo zrodlowe i analiza bazowa
Kod bazowej aplikacji iOS znajduje sie lokalnie w:
- `/mnt/d/projekty/tyflocentrum`

Wnioski z analizy tej aplikacji sa zebrane w:
- [Analiza aplikacji bazowej](docs/source-analysis.md)
- [Macierz parytetu funkcji](docs/feature-parity-matrix.md)

## Planowany uklad solution
- `src/Tyflocentrum.Windows.App`
- `src/Tyflocentrum.Windows.UI`
- `src/Tyflocentrum.Windows.Domain`
- `src/Tyflocentrum.Windows.Infrastructure`
- `tests/Tyflocentrum.Windows.Tests`
- `tests/Tyflocentrum.Windows.UITests`
- `docs/`

## Zasady pracy
- przed rozpoczeciem prac kodowych czytaj [docs/index.md](docs/index.md) i [AGENTS.md](AGENTS.md)
- istotne decyzje architektoniczne dokumentuj w `docs/adr/`
- zmiany w architekturze, dostepnosci lub procesie release aktualizuja tez dokumentacje

## Testowanie lokalne
- nie uruchamiaj bezposrednio `src/Tyflocentrum.Windows.App/bin/.../Tyflocentrum.Windows.App.exe`
- ten projekt jest obecnie budowany jako packaged desktop app i testowym artefaktem jest pakiet z katalogu `artifacts/AppPackages/` albo `artifacts/SignedAppPackages*/`
- do recznego testu na Windows uzywaj wygenerowanego `Install.ps1` lub `Add-AppDevPackage.ps1`
- najprostsza sciezka developerska:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\windows\Build-DevMsix.ps1 -Install`
- ta sciezka:
  - buduje nowy `MSIX`
  - usuwa poprzednia wersje pakietu testowego
  - instaluje nowy pakiet
- szczegoly sa opisane w [Setup deweloperski](docs/developer-setup.md) i [Pakowanie MSIX i Microsoft Store](docs/release-msix-store.md)
