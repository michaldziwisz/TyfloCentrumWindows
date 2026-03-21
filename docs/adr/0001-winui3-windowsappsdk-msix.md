# ADR 0001 - WinUI 3 + Windows App SDK + MSIX

## Status
Accepted

## Data
2026-03-19

## Kontekst
Projekt ma dostarczyc desktopowa wersje aplikacji TyfloCentrum dla Windows z naciskiem na:
- dostepnosc
- natywna integracje z systemem
- bezproblemowe pakowanie do Microsoft Store

Rozwazane byly podejscia wieloplatformowe i web-wrappery, ale glownym wymaganiem projektu jest wysoka przewidywalnosc zachowania dla czytnikow ekranu oraz stabilna droga do `MSIX`.

## Decyzja
Projekt bedzie realizowany w stacku:
- `WinUI 3`
- `Windows App SDK`
- `.NET 8`
- packaged desktop app z docelowym artefaktem `MSIX`

Architektura aplikacji pozostanie warstwowa:
- `App`
- `UI`
- `Domain`
- `Infrastructure`

## Uzasadnienie
- `WinUI 3` daje natywne kontrolki i najlepsza zgodnosc z ekosystemem Windows.
- `Windows App SDK` jest naturalnym wyborem dla nowej aplikacji desktopowej Microsoftu.
- `MSIX` jest zgodny z docelowym kanalem dystrybucji.
- Stack natywny zmniejsza ryzyko regresji dostepnosci wzgledem podejsc hybrydowych.

## Konsekwencje

### Pozytywne
- prostsza droga do Microsoft Store
- lepsza integracja z `UI Automation`
- bardziej przewidywalna obsluga klawiatury i czytnikow ekranu

### Negatywne
- brak wspolnego UI z iOS
- koniecznosc zaprojektowania oddzielnego UI desktopowego
- wieksza odpowiedzialnosc za natywne testy Windows

## Odrzucone opcje
- `.NET MAUI`
- Electron
- Tauri
- Avalonia

Powod odrzucenia:
- zbyt wysoki koszt ryzyka dostepnosciowego lub zbyt duza warstwa abstrakcji w projekcie, ktory ma byc Windows-first i Store-first.
