# Otwarte pytania i decyzje do zamkniecia

## Krytyczne przed startem implementacji

### 1. Zakres wspieranych wersji Windows
- Opcje:
  - tylko `Windows 11`
  - `Windows 10` i `Windows 11`
- Wplyw:
  - zakres testow
  - liczba workaroundow
  - koszt utrzymania

### 2. Push notifications w `1.0` czy `1.1`
- Wplyw:
  - architektura
  - pipeline release
  - testy
  - kontrakt push-service

### 3. Wymagalnosc `JAWS`
- Wplyw:
  - zakres QA
  - czas manualnych testow
  - kryteria release

### 4. Model renderowania HTML
- Opcje:
  - `WebView2`
  - renderer natywny
- Wplyw:
  - dostepnosc
  - bezpieczenstwo
  - koszt implementacji

### 5. Zakres glosowek na `1.0`
- Czy append jest obowiazkowy w pierwszej wersji?
- Czy brak trybu ucha jest akceptowalny na Windows?

### 6. `arm64` w pierwszym release
- Czy `arm64` ma wejsc od razu do `1.0`, czy moze byc gotowe przed submission Store?

### 7. Telemetria i diagnostyka
- Czy w `1.0` potrzebujemy tylko logowania technicznego, czy rowniez telemetry zdarzen?

## Decyzje juz podjete
- natywny stack Windows:
  - `WinUI 3`
  - `Windows App SDK`
  - `.NET 8`
- packaged app od poczatku
- docelowy artefakt:
  - `MSIX`
- priorytet czytnikow ekranu:
  - `Narrator`
  - `NVDA`

