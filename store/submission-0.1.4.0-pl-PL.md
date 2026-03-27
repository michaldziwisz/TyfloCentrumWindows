# TyfloCentrum 0.1.4.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.4.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.4.0_x64.msixupload`

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
W tej wersji rozbudowano pracę z podcastami i dostępność całej aplikacji. Z menu kontekstowego podcastu można teraz otworzyć same komentarze, znaczniki czasu i odnośniki bez wchodzenia do odtwarzacza. W samym playerze poprawiono kolejność komentarzy i odpowiedzi, sposób pokazywania szczegółów komentarza oraz widoczność kontrolek transportu przy obsłudze klawiaturą. Ujednolicono też akcje dla samych list znaczników i odnośników, tak aby oferowały te same operacje co odpowiednie listy w playerze, w tym ulubione, kopiowanie i udostępnianie.

Usprawniono też nawigację po listach treści. Dodano ustawienie określające, czy typ treści ma być pomijany, czytany przed nazwą albo po nazwie, poprawiono nawigację po pierwszej literze na listach nowości, podcastów, artykułów, wyników wyszukiwania i w spisie treści TyfloŚwiata, a także uproszczono etykiety odczytywane przez czytniki ekranu, dzięki czemu poruszanie się po listach jest szybsze i bardziej responsywne.

Poprawiono również pobieranie i prezentację treści: aktualności są teraz wyświetlane w prawidłowej kolejności chronologicznej, szybkie wielokrotne doładowywanie starszych treści nie powoduje już zamknięcia aplikacji, a dane z WordPressa są lokalnie cacheowane przez krótki czas, co zmniejsza liczbę zapytań do serwerów i przyspiesza działanie aplikacji.
```

## Changelog vs poprzednia opublikowana wersja
- dodane menu kontekstowe podcastu dla komentarzy, znaczników czasu i odnośników bez otwierania playera
- listy samych znaczników i odnośników mają teraz takie same akcje jak w playerze, w tym ulubione, kopiowanie i udostępnianie
- poprawiona prezentacja komentarzy podcastu:
  - kolejność komentarzy i odpowiedzi
  - lepsze oznaczanie odpowiedzi
  - szczegóły komentarza rozwijane bez błędu
- poprawiona widoczność kontrolek transportu odtwarzacza przy nawigacji klawiaturą
- dodane ustawienie `Typ treści na listach` z opcjami: brak typu, typ przed nazwą, typ po nazwie
- poprawiona nawigacja po pierwszej literze i etykiety czytników ekranu na listach treści
- poprawiona kolejność chronologiczna w `Aktualnościach`
- szybkie wielokrotne doładowywanie treści nie powoduje już zamknięcia aplikacji
- dodany lokalny cache dla odczytowych zapytań do WordPressa

## Product features
Na ten release nie trzeba ich zmieniać. Aktualna wersja jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.4.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
