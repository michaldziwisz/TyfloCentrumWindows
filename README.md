# TyfloCentrum Windows

Desktopowa wersja aplikacji TyfloCentrum dla systemu Windows, projektowana od początku z priorytetem na dostepnosc dla czytnikow ekranu i dystrybucje przez Microsoft Store.

Status repo:
- foundation aplikacji i shell `WinUI 3` są gotowe
- dokumentacja startowa jest gotowa
- działają już sekcje: `Nowości`, `Podcasty`, `Artykuły`, `Szukaj`, `Ulubione`, `Tyfloradio`
- listy `Nowości`, `Podcasty`, `Artykuły` i `Szukaj` mają domyślne akcje na `Enter` i menu kontekstowe dla akcji pobocznych zamiast osobnych przycisków w każdym wierszu
- listy `Nowości`, `Podcasty` i `Artykuły` dociągają automatycznie starsze treści podczas przewijania
- działają już komentarze podcastów z licznikiem i szczegółami komentarza oraz wbudowany odtwarzacz audio
- działa już pierwsza wersja wewnętrznej przeglądarki artykułów opartej o `WebView2`, w widoku czytania opartym o treść z API
- działają już skróty klawiaturowe dla głównych sekcji aplikacji i podstawowych akcji odtwarzacza
- player zapamiętuje pozycję podcastu i wznawia od ostatniego miejsca po ponownym otwarciu
- działa już pierwszy wariant dodatków playera: znaczniki czasu i odnośniki wyciągane z komentarzy podcastu, z kopiowaniem linków
- działa już rozszerzona wersja ulubionych z lokalnym JSON-em, filtrami `Podcasty/Artykuły/Tematy/Linki`, obsługą stron `TyfloŚwiata`, tematów odcinka i odnośników oraz kopiowaniem i udostępnianiem linków
- działa już formularz kontaktu tekstowego do Tyfloradia
- działa już pierwsza wersja głosówek z nagrywaniem `RAW`, appendem fragmentów, trybem `przytrzymaj i mów`, systemowym promptem zgody na mikrofon przy pierwszej próbie, komunikatami przerwań, odsłuchem i uploadem
- działa już sekcja `Ustawienia` z wyborem urządzenia wejściowego i wyjściowego, domyślną prędkością odtwarzania i zapamiętywaniem ostatniej prędkości
- działają już lokalne powiadomienia Windows o nowych artykułach i podcastach, z osobnymi przełącznikami w `Ustawieniach`; monitorowanie działa, gdy aplikacja jest uruchomiona
- działa już kliencka warstwa WNS pod powiadomienia przy zamkniętej aplikacji: rejestracja kanału, synchronizacja z `push-service`, obsługa kliknięcia toastu i routing do artykułu albo podcastu; pełne E2E wymaga jeszcze zewnętrznej konfiguracji Azure/WNS i wysyłki po stronie backendu
- repo zawiera juz tez osobny backend `TyfloCentrum.PushService` dla Windows `WNS`, z pollingiem `WordPress`, endpointami `register/update/unregister`, eventami webhook i realna wysylka do `WNS`; do produkcji brakuje jeszcze tylko konfiguracji sekretow Azure i wdrozenia na VPS
- działają już pobierania z menu kontekstowego: podcasty zapisują się jako `mp3`, a artykuły jako pojedynczy plik `html` z osadzonymi obrazami; w `Ustawieniach` można wskazać folder docelowy albo użyć domyślnego folderu `Pobrane`
- działa już pierwszy wariant `TyfloŚwiata`: roczniki, numery czasopisma, PDF, spis treści artykułów z `pages`, szybkie dodawanie stron do ulubionych i udostępnianie linków
- działa już osobna sekcja `Zgłoś błąd lub sugestię`, która wysyła publiczne issue `GitHub` z bezpieczną diagnostyką jawną i opcjonalnym prywatnym logiem technicznym do `Sygnalisty`
- repo ma juz tez drugi kanal dystrybucji poza Store: instalator `EXE`, budowany z tej samej bazy kodu na podstawie dzialajacego pakietu `MSIX`
- repo ma juz tez workflow GitHub Actions `Direct EXE Build`, ktory buduje instalator poza Store i zapisuje go jako artefakt
- listy treści oraz wybrane widoki wspierają już skróty `Ctrl+S` do pobierania i `Ctrl+U` do udostępniania, a komunikaty o zmianach ulubionych są ogłaszane także czytnikom ekranu
- docelowy stack: `WinUI 3`, `Windows App SDK`, `.NET 8`, `MSIX`

## Cele produktu
- odtworzyc funkcjonalnosc obecnej aplikacji iOS `TyfloCentrum`
- zapewnic wysoka jakosc obslugi `Narrator` i `NVDA`
- dostarczyc pakiet `MSIX` gotowy do publikacji w Microsoft Store

## Najwazniejsze dokumenty
- [Mapa dokumentacji](docs/index.md)
- [Plan i backlog wdrozenia](docs/windows-plan-and-backlog.md)
- [Wymagania produktowe](docs/product-requirements.md)
- [Architektura](docs/architecture.md)
- [Wymagania dostepnosci](docs/accessibility-requirements.md)
- [Integracje API](docs/api-integrations.md)
- [Push Service dla Windows](docs/push-service-windows.md)
- [Strategia testow](docs/testing-strategy.md)
- [Setup deweloperski](docs/developer-setup.md)
- [Release do MSIX i Microsoft Store](docs/release-msix-store.md)
- [Dystrybucja poza Store jako direct EXE](docs/release-direct-exe.md)
- [GitHub Pages dla polityki prywatności](site/privacy/index.html)

## Repo zrodlowe i analiza bazowa
Kod bazowej aplikacji iOS znajduje sie lokalnie w:
- `/mnt/d/projekty/tyflocentrum`

Wnioski z analizy tej aplikacji sa zebrane w:
- [Analiza aplikacji bazowej](docs/source-analysis.md)
- [Macierz parytetu funkcji](docs/feature-parity-matrix.md)

## Planowany uklad solution
- `src/TyfloCentrum.Windows.App`
- `src/TyfloCentrum.Windows.UI`
- `src/TyfloCentrum.Windows.Domain`
- `src/TyfloCentrum.Windows.Infrastructure`
- `tests/TyfloCentrum.Windows.Tests`
- `tests/TyfloCentrum.Windows.UITests`
- `docs/`

## Zasady pracy
- przed rozpoczeciem prac kodowych czytaj [docs/index.md](docs/index.md) i [AGENTS.md](AGENTS.md)
- istotne decyzje architektoniczne dokumentuj w `docs/adr/`
- zmiany w architekturze, dostepnosci lub procesie release aktualizuja tez dokumentacje

## Testowanie lokalne
- nie uruchamiaj bezposrednio `src/TyfloCentrum.Windows.App/bin/.../TyfloCentrum.Windows.App.exe`
- ten projekt jest obecnie budowany jako packaged desktop app i testowym artefaktem jest pakiet z katalogu `artifacts/AppPackages/` albo `artifacts/SignedAppPackages*/`
- do recznego testu na Windows uzywaj wygenerowanego `Install.ps1` lub `Add-AppDevPackage.ps1`
- najprostsza sciezka developerska:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\windows\Build-DevMsix.ps1 -Install`
- ta sciezka:
  - buduje nowy `MSIX`
  - generuje odpowiadajaca mu paczke symboli `.appxsym`
  - usuwa poprzednia wersje pakietu testowego
  - instaluje nowy pakiet
- alternatywnie, dla kanalu poza Store:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\windows\Build-DirectSetupExe.ps1`
- ten workflow generuje instalator `EXE` oparty o dzialajacy pakiet `MSIX`
- szczegoly sa opisane w [Setup deweloperski](docs/developer-setup.md) i [Pakowanie MSIX i Microsoft Store](docs/release-msix-store.md)
