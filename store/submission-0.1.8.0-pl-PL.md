# TyfloCentrum 0.1.8.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.8.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.8.0_x64.msixupload`

Jesli budujesz go lokalnie, przygotuj go tak:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\New-StoreMsixUpload.ps1 -PackageDirectory .\artifacts\SignedAppPackages\TyfloCentrum.Windows.App_0.1.8.0_x64_Test
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
W tej wersji poprawiono wygodę i dostępność odtwarzacza podcastów. Pełne nazwy przycisków dodatków odcinka oraz przycisku przesyłania do urządzenia są teraz zawsze widoczne, bez ucinania tekstu. Dzięki temu łatwiej od razu rozpoznać akcje takie jak komentarze, znaczniki czasu, odnośniki czy przesyłanie dźwięku do urządzenia zewnętrznego.

Usprawniono też nagrywanie głosówek do Tyfloradia. Po rozpoczęciu nagrywania fokus przechodzi od razu na przycisk zatrzymania, co ułatwia szybką kontrolę nagrania z klawiatury i czytnika ekranu. Dodatkowo finalny plik głosówki jest teraz przygotowywany jako monofoniczny, również wtedy, gdy mikrofon wejściowy działa w trybie dwukanałowym.

W tle poprawiono też przetwarzanie dźwięku i testy regresyjne związane z formatem WAV, tak aby nagrania były lżejsze, spójniejsze i lepiej dopasowane do rzeczywistego zastosowania głosówek w aplikacji.
```

## Changelog vs poprzednia opublikowana wersja
- poprawione przyciski w odtwarzaczu, tak aby ich pełne nazwy były zawsze widoczne
- poprawione przyciski dodatków odcinka w playerze:
  - `Dodaj komentarz`
  - `Pokaż komentarze`
  - `Pokaż znaczniki czasu`
  - `Pokaż odnośniki`
- poprawiony fokus po rozpoczęciu nagrywania głosówki:
  - domyślnie trafia teraz na `Zatrzymaj nagrywanie`
- finalny plik głosówki jest teraz monofoniczny
- stereofoniczne wejście mikrofonowe jest monofonizowane podczas przygotowania finalnego nagrania
- profil wynikowego `m4a` jest wymuszony na jeden kanał

## Product features
Na ten release nie trzeba ich zmieniać. Aktualna wersja jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.8.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
