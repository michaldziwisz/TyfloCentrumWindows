# Screenshoty do Microsoft Store

## Minimalny zestaw na start
Jeśli na pierwszą submission chcesz dodać tylko jeden screenshot, zrób:

- ekran `Nowości`,
- z widoczną lewą nawigacją,
- z widoczną listą treści po prawej,
- bez komunikatów debugowych, bez prywatnych danych i bez przypadkowo otwartych menu.

## Zalecany plik
- `store/assets/screenshots/pl-PL/01-nowosci.png`

## Jak zrobić pierwszy screenshot
1. Uruchom aplikację `TyfloCentrum`.
2. Przejdź do sekcji `Nowości`.
3. Ustaw okno aplikacji na czytelny, średni lub duży rozmiar.
4. Upewnij się, że lewa nawigacja i lista treści są jednocześnie widoczne.
5. Zrób zrzut do pliku PNG.
6. Zapisz go pod ścieżką `store/assets/screenshots/pl-PL/01-nowosci.png`.

## Automatyzacja
Możesz też użyć skryptu:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\windows\Capture-StoreScreenshot.ps1
```

Skrypt:
- uruchamia `TyfloCentrum`, jeśli aplikacja nie jest jeszcze otwarta,
- przełącza aplikację na `Nowości`,
- zapisuje screenshot do `store/assets/screenshots/pl-PL/01-nowosci.png`.

## Dodatkowe warianty, jeśli będziesz chciał więcej
- `02-podcasty-player.png` — podcasty z otwartym odtwarzaczem,
- `03-artykuly-reader.png` — artykuł otwarty w widoku czytania,
- `04-tyfloradio.png` — ekran Tyfloradia,
- `05-ulubione.png` — lista ulubionych elementów.

## Uwagi
- Microsoft Store oczekuje prawdziwych zrzutów działającej aplikacji, nie samych grafik brandingowych.
- Logo z katalogu `Assets` nie zastępuje screenshotu aplikacji.
