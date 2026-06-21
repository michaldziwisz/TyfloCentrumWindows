# TyfloCentrum 0.1.14.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.14.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.14.0_x64.msixupload`

Jesli budujesz go lokalnie, przygotuj go tak:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\New-StoreMsixUpload.ps1 -PackageDirectory .\artifacts\SignedAppPackages\TyfloCentrum.Windows.App_0.1.14.0_x64_Debug_Test
```

Do `msixupload` trafia tylko `.msix` oraz opcjonalnie `.appxsym`. Plik `.cer` musi zostac poza archiwum.

## Description
Poczatek opisu powinien pozostac bez zmian, zeby nie wrocilo ostrzezenie o zaleznosci od runtime:

```text
TyfloCentrum wymaga do działania Microsoft .NET Desktop Runtime.
TyfloCentrum to dostępna aplikacja dla systemu Windows, która zbiera w jednym miejscu treści z ekosystemu TyfloPodcast. Pozwala wygodnie przeglądać aktualności, artykuły i podcasty, słuchać TyfloRadia, korzystać z ulubionych, pobierać materiały do czytania i odsłuchu offline oraz wysyłać wiadomości tekstowe i głosowe do redakcji.
```

Reszte opisu skopiuj z:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## What's new in this version
```text
Teraz możesz napisać wiadomość tekstową do Tyfloradia bez wychodzenia z odtwarzacza. Gdy na antenie trwa audycja interaktywna, w odtwarzaczu Tyfloradia pojawia się przycisk pisania wiadomości, który rozwija formularz wprost w oknie odtwarzacza. Po wysłaniu wiadomości formularz zwija się, a fokus wraca na przycisk i czytnik ekranu potwierdza wysyłkę. Gdy nie trwa audycja interaktywna, przycisk pisania wiadomości nie jest pokazywany.
```

## Changelog vs poprzednia opublikowana wersja
- w odtwarzaczu Tyfloradia podczas audycji interaktywnej dostępny jest przycisk `Napisz wiadomość do Tyfloradia`
- formularz wiadomości tekstowej rozwija się bezpośrednio w oknie odtwarzacza, bez zamykania transmisji na żywo
- po wysłaniu wiadomości formularz zwija się, fokus wraca na przycisk, a czytnik ekranu ogłasza `Wiadomość wysłana pomyślnie.`
- przycisk pisania wiadomości pojawia się tylko wtedy, gdy na antenie trwa audycja interaktywna, zgodnie z istniejącą regułą kontaktu z sekcji Tyfloradio
- formularz korzysta z tej samej walidacji i zapisu szkicu co kontakt tekstowy z sekcji Tyfloradio
- głosówki celowo pozostają dostępne tylko w sekcji Tyfloradio, a nie w odtwarzaczu
- uzupełniono testy fabryki żądań odtwarzania i widoku Tyfloradia o nową obsługę kontaktu z odtwarzacza

## Product features
Warto zaktualizować listing funkcji, bo pojawia się nowa funkcja użytkowa:

- `Wiadomość tekstowa do Tyfloradia także z poziomu odtwarzacza podczas audycji interaktywnej.`

Pełna aktualna wersja listingu jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.14.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- `Product features`: dodaj informację o pisaniu wiadomości z odtwarzacza Tyfloradia
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
