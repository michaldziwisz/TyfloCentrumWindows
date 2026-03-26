# TyfloCentrum 0.1.3.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.3.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.3.0_x64.msixupload`

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
W tej wersji dodano nową sekcję „Zgłoś błąd lub sugestię”, która pozwala szybko wysłać zgłoszenie bezpośrednio z poziomu aplikacji. Publiczne zgłoszenie trafia do repozytorium projektu, a opcjonalne dane techniczne i adres e-mail są przekazywane wyłącznie do prywatnego kanału diagnostycznego po wyrażeniu zgody.

Uproszczono też pracę z treściami i dostępność interfejsu: artykuły otwierają się już bezpośrednio w readerze, bez osobnego widoku szczegółów. Dodano zapamiętywanie głośności wspólne dla podcastów i Tyfloradia, kopiowanie adresów artykułów oraz stron i plików podcastów, a także rozszerzono skróty klawiaturowe dla pobierania, udostępniania i kopiowania.

Poprawiono również komunikaty dla czytników ekranu przy dodawaniu i usuwaniu ulubionych, przebudowano skróty w odtwarzaczu tak, aby Alt+strzałki zmieniały prędkość, a Ctrl+strzałki w górę i dół regulowały głośność, oraz poprawiono informowanie o sprawdzaniu dostępu do mikrofonu przy starcie nagrywania głosówki.
```

## Changelog vs poprzednia opublikowana wersja
- dodana sekcja `Zgłoś błąd lub sugestię` z wysyłką zgłoszeń z poziomu aplikacji
- artykuły otwierają się bezpośrednio w readerze, bez osobnego ekranu szczegółów
- dodane globalne zapamiętywanie głośności dla podcastów i `Tyfloradia`
- dodane kopiowanie adresu artykułu, strony podcastu i pliku podcastu
- rozszerzone skróty klawiaturowe:
  - `Ctrl+S` pobieranie
  - `Ctrl+U` udostępnianie
  - `Ctrl+C` kopiowanie adresu strony
  - `Ctrl+P` kopiowanie adresu pliku podcastu
  - `Alt+strzałka w górę/dół` zmiana prędkości
  - `Ctrl+strzałka w górę/dół` zmiana głośności
- poprawione komunikaty dla czytników ekranu przy ulubionych
- poprawiony start nagrywania głosówek i komunikat o sprawdzaniu dostępu do mikrofonu

## Product features
Na ten release nie trzeba ich zmieniać. Aktualna wersja jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień tylko pakiet na `0.1.3.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
