# TyfloCentrum 0.1.11.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.11.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.11.0_x64.msixupload`

Jesli budujesz go lokalnie, przygotuj go tak:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\New-StoreMsixUpload.ps1 -PackageDirectory .\artifacts\SignedAppPackages\TyfloCentrum.Windows.App_0.1.11.0_x64_Test
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
W tej wersji poprawiono zachowanie odtwarzacza podcastów na komputerach z wysokim skalowaniem Windows, mniejszą rozdzielczością albo nietypowym rozmiarem okna. Dolne sekcje odtwarzacza, w tym skróty, dodatki odcinka, komentarze, formularz komentarza i przyciski akcji, nie powinny być już ucinane; zawartość można przewinąć.
```

## Changelog vs poprzednia opublikowana wersja
- odtwarzacz podcastów nie ucina dolnych sekcji przy wysokim skalowaniu Windows, mniejszej rozdzielczości albo nietypowym rozmiarze okna
- zawartość odtwarzacza można przewinąć do skrótów, dodatków odcinka, komentarzy, formularza komentarza i przycisków akcji

## Product features
Na ten release nie trzeba ich zmieniać. Aktualna wersja jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.11.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
