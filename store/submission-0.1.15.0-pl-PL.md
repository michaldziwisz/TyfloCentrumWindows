# TyfloCentrum 0.1.15.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.15.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.15.0_x64.msixupload`

Jesli budujesz go lokalnie, przygotuj go tak:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\New-StoreMsixUpload.ps1 -PackageDirectory .\artifacts\SignedAppPackages\TyfloCentrum.Windows.App_0.1.15.0_x64_Debug_Test
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
Odtwarzanie podcastów jest teraz szybsze i płynniejsze. Odcinek zaczyna grać niemal od razu, bez czekania na pobranie całego pliku, a przewijanie w przód i w tył działa bez zacinania nawet w długich audycjach. Zmiana dotyczy podcastów; Tyfloradio na żywo działa jak dotychczas.
```

## Changelog vs poprzednia opublikowana wersja
- podcasty (nie `Tyfloradio` na żywo) grają teraz przez progresywny bufor: jedno ciągłe pobranie pliku, z którego odtwarzacz czyta zakresami
- odtwarzanie startuje po dociągnięciu początku pliku, a nie po pobraniu całości — koniec kilkudziesięciosekundowego „mulenia" na starcie długich odcinków
- przewijanie w przód i w tył w obrębie pobranego fragmentu jest natychmiastowe i nie wywołuje ponownego pobierania (serwer liczy jedno pobranie na odtworzenie zamiast wielu pełnych)
- przewinięcie poza pobrany fragment dobiera brakujące dane osobnym, małym żądaniem zakresu i gra dalej bez błędu
- zamknięcie odtwarzacza albo przełączenie odcinka przerywa pobieranie w tle i sprząta plik tymczasowy
- Tyfloradio na żywo pozostaje przy bezpośrednim strumieniu, bez zmian

## Product features
Warto zaktualizować listing funkcji, bo pojawia się nowa cecha użytkowa:

- `Szybki start odtwarzania podcastów i płynne przewijanie bez czekania na pobranie całego pliku.`

Pełna aktualna wersja listingu jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.15.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- `Product features`: dodaj informację o szybszym odtwarzaniu podcastów
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
