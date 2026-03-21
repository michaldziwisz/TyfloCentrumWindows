# Wymagania produktowe

## Cel produktu
Zbudowac aplikacje desktopowa dla systemu Windows, ktora zapewnia funkcjonalnosc rownowazna obecnej aplikacji iOS `TyfloCentrum`, z naciskiem na:
- dostepnosc dla czytnikow ekranu
- wygodna obsluge klawiatura
- odtwarzanie audio i live streamu
- bezproblemowa dystrybucje przez Microsoft Store

## Uzytkownicy docelowi
- osoby niewidome korzystajace z `Narrator`, `NVDA` lub `JAWS`
- osoby slabowidzace korzystajace z wysokiego kontrastu i wiekszego tekstu
- uzytkownicy klawiatury bez myszy
- sluchacze Tyflopodcast i Tyfloradia na komputerach Windows

## Glowny zakres funkcjonalny `1.0`

### Tresci
- wspolny feed nowosci
- przegladanie kategorii podcastow
- przegladanie kategorii artykulow
- listy wpisow i szczegoly
- strony i numery TyfloSwiata
- wyszukiwarka
- komentarze do podcastow

### Audio
- odtwarzanie podcastow
- odtwarzanie live streamu Tyfloradia
- seek `+-30s`
- zmiana predkosci do `3.0x`
- wznawianie pozycji
- znaczniki czasu i linki powiazane

### Funkcje osobiste
- ulubione
- ustawienia odczytu i odtwarzania

### Kontakt z radiem
- kontakt tekstowy
- kontakt glosowy
- sprawdzanie, czy audycja interaktywna jest aktywna
- ramowka

### Dystrybucja
- paczka `MSIX`
- gotowosc do publikacji w Microsoft Store

## Wymagania niefunkcjonalne
- zgodnosc z `WCAG 2.2 AA` dla czesci, ktore da sie ocenic na poziomie aplikacji desktopowej
- poprawna obsluga `Narrator` i `NVDA`
- przewidywalny focus order i zachowanie klawiatury
- sensowne stany `loading`, `error`, `empty`
- odporna obsluga bledow sieciowych
- architektura pozwalajaca testowac logike bez UI

## Wymagania wydaniowe
- `x64` jest wymagane dla `1.0`
- `arm64` jest bardzo wskazane i powinno wejsc najpozniej przed publikacja Store
- release candidate przechodzi:
  - testy jednostkowe
  - smoke testy UI
  - testy manualne `Narrator`
  - testy manualne `NVDA`
  - test wysokiego kontrastu
  - test zwiekszonego tekstu

## Poza zakresem pierwszego MVP
- pelne wsparcie wszystkich screen readerow poza `Narrator` i `NVDA`
- rozbudowana telemetria
- tryb offline dla tresci
- synchronizacja miedzy urzadzeniami

## Miary sukcesu
- uzytkownik niewidomy jest w stanie samodzielnie:
  - uruchomic aplikacje
  - odszukac nowa audycje
  - odtworzyc podcast
  - przeskoczyc do znacznika czasu
  - odczytac artykul
  - wyslac wiadomosc do radia
- aplikacja przechodzi publikacje w Microsoft Store bez zmiany modelu pakowania
