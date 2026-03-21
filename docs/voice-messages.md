# Głosówki w Windows

## Status
Stan na `2026-03-19`:
- pierwsza wersja głosówek dla Windows jest zaimplementowana
- działa:
  - wpisanie podpisu
  - nagranie jednego pliku audio
  - dogrywanie kolejnych fragmentów do istniejącego nagrania
  - tryb `przytrzymaj i mów`
  - komunikowanie przerwania nagrywania i utraty mikrofonu
  - odsłuch nagrania
  - usunięcie nagrania
  - upload `multipart/form-data` do `json.php?ac=addvoice`
- jeszcze nie działa:
  - tryb `ucha`

## Najważniejsze założenie audio
Aplikacja najpierw próbuje nagrywać w trybie `RAW`.

To oznacza:
- priorytetem jest surowe wejście mikrofonowe bez systemowych ulepszeń typu:
  - `AEC`
  - `AGC`
  - redukcja szumu
- jeśli urządzenie nie obsługuje `RAW`, aplikacja cicho przechodzi na najlepszy tryb dostępny na danym urządzeniu
- użytkownik nie dostaje z tego powodu dodatkowego komunikatu ani błędu

## Użyte API
- `MediaCapture`
- `MediaCaptureInitializationSettings.AudioProcessing = Raw`
- `StreamingCaptureMode.Audio`
- zapis do `.m4a`
- `MediaComposition` do scalania segmentów w jeden plik końcowy
- lokalny odsłuch przez `MediaPlayer`

## Dlaczego tak
To jest kompromis między wymaganiem jakościowym a odpornością aplikacji:
- materiał ma być pobrany możliwie bez ingerencji systemu
- jeśli urządzenie pozwala, aplikacja używa `RAW`
- jeśli nie pozwala, ważniejsze jest dostarczenie nagrania niż techniczne wymuszenie błędu

## Ograniczenia bieżącej wersji
- po każdym dograniu aplikacja przez chwilę przygotowuje finalny plik `.m4a`
- jeśli scalenie nowego fragmentu się nie powiedzie, poprzednia wersja nagrania zostaje zachowana
- systemowe przerwanie nagrywania zatrzymuje bieżący fragment i ogłasza komunikat błędu; aplikacja nie wznawia nagrania automatycznie
- zamknięcie dialogu czyści tymczasowy plik audio
- maksymalna długość po stronie klienta pozostaje `20 minut`
- finalna walidacja długości i rozmiaru nadal należy do backendu

## Dalsze kroki
- dodać czytelne komunikaty dla:
  - braku mikrofonu
  - braku uprawnień
  - utraty urządzenia wejściowego
- dodać smoke test UI dla scenariusza:
  - otwarcie formularza głosówki
  - rozpoczęcie nagrywania
  - zatrzymanie
  - wysyłka
