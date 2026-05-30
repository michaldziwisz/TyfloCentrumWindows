# TyfloCentrum 0.1.13.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.13.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.13.0_x64.msixupload`

Jesli budujesz go lokalnie, przygotuj go tak:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\New-StoreMsixUpload.ps1 -PackageDirectory .\artifacts\SignedAppPackages\TyfloCentrum.Windows.App_0.1.13.0_x64_Test
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
Dodano obsługę tekstowych wersji audycji. Jeśli odcinek ma dostępną wersję tekstową, aplikacja wykrywa ją automatycznie i pokazuje opcję otwarcia z menu kontekstowego podcastu oraz z odtwarzacza. Treść jest wyświetlana jako renderowany HTML, dzięki czemu zachowuje linki i pozwala korzystać z wyszukiwania w tekście.
```

## Changelog vs poprzednia opublikowana wersja
- aplikacja wykrywa linki do tekstowych wersji audycji w treści wpisów podcastowych
- menu kontekstowe podcastu pokazuje `Pokaż wersję tekstową`, jeśli odcinek ma taką wersję
- odtwarzacz podcastów pokazuje przycisk `Pokaż wersję tekstową` dla odcinków z tekstem
- wersja tekstowa jest renderowana jako HTML, z zachowaniem linków i obsługi wyszukiwania w treści
- pobieranie tekstowych wersji obsługuje linki WordPress po `page_id` oraz po slugu strony
- uzupełniono testy parsera i usług pobierających tekstową wersję audycji

## Product features
Warto zaktualizować listing funkcji, bo pojawia się nowa funkcja użytkowa:

- `Tekstowe wersje audycji dostępne z menu podcastu i odtwarzacza.`

Pełna aktualna wersja listingu jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.13.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- `Product features`: dodaj informację o tekstowych wersjach audycji
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
