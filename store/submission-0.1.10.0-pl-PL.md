# TyfloCentrum 0.1.10.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.10.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.10.0_x64.msixupload`

Jesli budujesz go lokalnie, przygotuj go tak:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\New-StoreMsixUpload.ps1 -PackageDirectory .\artifacts\SignedAppPackages\TyfloCentrum.Windows.App_0.1.10.0_x64_Test
```

Do `msixupload` trafia tylko `.msix` oraz opcjonalnie `.appxsym`. Plik `.cer` musi zostac poza archiwum.

## Description
Początek opisu powinien pozostać bez zmian, żeby nie wróciło ostrzeżenie o zależności od runtime:

```text
TyfloCentrum wymaga do działania Microsoft .NET Desktop Runtime.
TyfloCentrum to dostępna aplikacja dla systemu Windows, która zbiera w jednym miejscu treści z ekosystemu TyfloPodcast. Pozwala wygodnie przeglądać aktualności, artykuły i podcasty, słuchać TyfloRadia, korzystać z ulubionych, pobierać materiały do czytania i odsłuchu offline oraz wysyłać wiadomości tekstowe i głosowe do redakcji.
```

Resztę opisu skopiuj z:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## What's new in this version
```text
W tej wersji ujednolicono formularz dodawania komentarzy pod podcastami w odtwarzaczu z formularzem dostępnym w osobnym widoku komentarzy. Pola edycji mają stabilną minimalną szerokość i rozciągają się razem z panelem, dzięki czemu nie powinny zwężać się na komputerach z wysokim skalowaniem, małą rozdzielczością albo nietypowym rozmiarem okna.

Zmiana dotyczy pól Imię, Adres e-mail oraz Treść komentarza i ma poprawić wygodę wpisywania komentarzy z klawiatury oraz czytnika ekranu.

Usprawniono też diagnostykę zgłoszeń technicznych: raporty rozróżniają teraz wersję instalowaną klasycznym instalatorem od wersji ze Store, a problemy z uruchomieniem mikrofonu zapisują w logu bezpieczne informacje techniczne potrzebne do ustalenia przyczyny.
```

## Changelog vs poprzednia opublikowana wersja
- poprawiono layout formularza komentarzy osadzonego w odtwarzaczu podcastu
- dodano minimalną szerokość pól `Imię`, `Adres e-mail` i `Treść komentarza` także w playerze
- wymuszono rozciąganie panelu komentarza i pól edycji w odtwarzaczu
- rozszerzono scenariusz ręcznej walidacji formularza o dialog komentarzy i player
- poprawiono raportowanie wersji i typu instalacji w diagnostyce zgłoszeń
- dodano bezpieczne logowanie etapu i typu awarii przy uruchamianiu mikrofonu

## Product features
Na ten release nie trzeba ich zmieniać. Aktualna wersja jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.10.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
