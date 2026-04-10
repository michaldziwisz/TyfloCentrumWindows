# TyfloCentrum 0.1.7.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.7.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.7.0_x64.msixupload`

Jesli budujesz go lokalnie, przygotuj go tak:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\New-StoreMsixUpload.ps1 -PackageDirectory .\artifacts\SignedAppPackages\TyfloCentrum.Windows.App_0.1.7.0_x64_Test
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
W tej wersji rozbudowano obsługę komentarzy podcastów. Można już czytać komentarze bezpośrednio z menu kontekstowego podcastu, dodawać nowe komentarze i odpowiadać na istniejące zarówno z poziomu listy komentarzy, jak i bezpośrednio w odtwarzaczu. Aplikacja pokazuje też wynik publikacji komentarza zgodnie z odpowiedzią WordPressa, na przykład że komentarz został opublikowany, przekazany do moderacji albo zakwalifikowany jako spam.

Usprawniono również sam formularz komentarza i jego dostępność. Formularz nie otwiera się już automatycznie przy samym wejściu w komentarze, po udanym wysłaniu zamyka się, fokus wraca w logiczne miejsce, a pola formularza są czytane krócej i bez dublowania komunikatów. Uproszczono też widok komentarzy, usuwając zbędny przycisk szczegółów i zostawiając rozwijanie treści bezpośrednio na komentarzu.

Poprawiono też stabilność dodatków podcastu. Komentarze, znaczniki czasu i odnośniki uruchamiane z menu kontekstowego podcastu działają teraz spójnie z widokiem w odtwarzaczu, a sam odtwarzacz i listy komentarzy lepiej współpracują z klawiaturą oraz czytnikami ekranu.
```

## Changelog vs poprzednia opublikowana wersja
- dodane wysyłanie komentarzy WordPress bezpośrednio z aplikacji
- dodane odpowiadanie na komentarze podcastów
- dodane komunikaty o wyniku publikacji komentarza zgodne z odpowiedzią WordPressa:
  - opublikowany
  - przekazany do moderacji
  - zakwalifikowany jako spam
- komentarze można otwierać i obsługiwać zarówno z menu kontekstowego podcastu, jak i w odtwarzaczu
- formularz komentarza nie otwiera się już automatycznie przy wejściu w komentarze
- po udanym wysłaniu formularz komentarza zamyka się, a fokus wraca na listę komentarzy albo przycisk dodania komentarza
- uproszczone i krótsze etykiety pól formularza komentarza
- usunięty zbędny przycisk `Szczegóły komentarza`; rozwijanie treści działa bezpośrednio na komentarzu
- poprawione odczyty przycisku `Odpowiedz`, tak aby czytnik nie odczytywał ponownie całej treści komentarza

## Product features
Na ten release nie trzeba ich zmieniać. Aktualna wersja jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.7.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
