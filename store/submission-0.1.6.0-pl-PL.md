# TyfloCentrum 0.1.6.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.6.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.6.0_x64.msixupload`

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
W tej wersji poprawiono czytanie artykułów, Tyfloradio i odtwarzacz. Czytnik artykułów działa teraz czyściej z klawiaturą i czytnikami ekranu, bez zbędnych przycisków w cyklu tabulacji, a fokus trafia bezpośrednio do treści.

Rozbudowano też odtwarzacz: pasek transportu jest stale widoczny wizualnie, przycisk Przesyłaj do urządzenia wrócił do interfejsu, a sterowanie odtwarzaniem jest bardziej przewidywalne przy obsłudze samą klawiaturą.

Usprawniono również sekcję Tyfloradio i długie listy treści. Komunikaty przy próbie kontaktu poza audycją interaktywną są teraz spójne i czytelne, Pokaż ramówkę Tyfloradia otwiera pełną treść ramówki w polu tylko do odczytu, podczas automatycznego doczytywania starszych pozycji aplikacja lepiej zachowuje fokus na aktualnym elemencie, a sama aplikacja uruchamia się od razu w zmaksymalizowanym oknie.
```

## Changelog vs poprzednia opublikowana wersja
- czytnik artykułów działa bez dodatkowych przycisków `Odśwież` i `Otwórz w przeglądarce` w tab order
- czytnik artykułów ma poprawione zachowanie fokusu i krótką nazwę hosta dla czytników ekranu
- odtwarzacz dostał jawnie widoczny pasek transportu z przyciskami przewijania, `Odtwarzaj/Pauza`, suwakiem pozycji i przyciskiem `Przesyłaj do urządzenia`
- sekcja `Tyfloradio` pokazuje pełną ramówkę w wielowierszowym polu tylko do odczytu
- komunikaty przy próbie kontaktu z Tyfloradiem poza audycją interaktywną są spójne i bez technicznego `Ikona błędu`
- podczas doczytywania starszych treści aplikacja zachowuje fokus na aktualnie zaznaczonym wpisie
- formularz zgłoszeń używa natywnego wielowierszowego pola edycji dla opisu publicznego
- aplikacja uruchamia się w oknie zmaksymalizowanym

## Product features
Na ten release nie trzeba ich zmieniać. Aktualna wersja jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.6.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
