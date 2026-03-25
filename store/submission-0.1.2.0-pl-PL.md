# TyfloCentrum 0.1.2.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.2.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.2.0_x64.msixupload`

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

Uproszczono też pracę z treściami i dostępność interfejsu: artykuły otwierają się już bezpośrednio w readerze, bez osobnego widoku szczegółów. Poprawiono komunikaty dla czytników ekranu przy dodawaniu i usuwaniu ulubionych, rozszerzono skróty klawiaturowe o Ctrl+S dla pobierania i Ctrl+U dla udostępniania, a także poprawiono informowanie o sprawdzaniu dostępu do mikrofonu przy starcie nagrywania głosówki.
```

## Product features
Na ten release nie trzeba ich zmieniać. Aktualna wersja jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień tylko pakiet na `0.1.2.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
