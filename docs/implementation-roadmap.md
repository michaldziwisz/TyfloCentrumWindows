# Roadmapa implementacji

## Cel
Ustalic kolejnosc wdrozenia, ktora najpierw zamyka ryzyka techniczne i dostepnosciowe, a dopiero potem dowozi pelny parity funkcji.

## Etap 0 - Dokumentacja i decyzje startowe
Rezultat:
- komplet dokumentacji startowej
- backlog
- ADR stacku
- lista otwartych decyzji

Wyjscie:
- mozna rozpoczac scaffolding solution

## Etap 1 - Spike'i platformowe
Zakres:
- shell `WinUI 3`
- HTML reader
- player audio
- glosowki
- decyzja o push

Wyjscie:
- zamkniete najwieksze ryzyka implementacyjne

## Etap 2 - Foundation
Zakres:
- solution
- DI
- logging
- shell
- standardy UI i a11y
- podstawowa warstwa HTTP

Wyjscie:
- gotowy szkielet do rozwoju modulow funkcjonalnych

## Etap 3 - Content MVP
Zakres:
- nowosci
- podcasty
- artykuly
- wyszukiwarka
- szczegoly
- komentarze
- ulubione

Wyjscie:
- aplikacja pozwala konsumowac tresci bez kontaktu glosowego i push

Stan:
- pierwsza wersja etapu jest już gotowa:
  - feed nowości
  - katalogi podcastów i artykułów
  - pierwszy wariant `TyfloŚwiata` z numerami czasopisma i stronami
  - wyszukiwarka
  - szczegóły wpisów
  - komentarze podcastów
  - sekcja ulubionych z lokalnym JSON-em, stronami `TyfloŚwiata`, tematami i linkami

## Etap 4 - Audio i Tyfloradio
Zakres:
- pelny player
- live stream
- ramowka
- kontakt tekstowy

Wyjscie:
- zamkniete glowne sciezki audio

Stan:
- pierwsza wersja etapu jest już gotowa:
  - wbudowany odtwarzacz podcastów i live streamu
  - skoki `30 s`
  - zmiana prędkości
  - skróty klawiaturowe odtwarzacza
  - wznawianie podcastu od ostatniej zapisanej pozycji
  - dodatki odcinka z komentarzy: znaczniki czasu i odnośniki
  - status Tyfloradia
  - ramówka
  - kontakt tekstowy

## Etap 5 - Głosowki i parity
Zakres:
- recorder
- upload `multipart/form-data`
- finalne ustawienia parity

Wyjscie:
- parity funkcjonalne z iOS dla obszaru kontaktu z radiem

Stan:
- pierwsza wersja dla Windows jest już gotowa:
  - `RAW` capture
  - append kilku fragmentów do jednego pliku końcowego
  - `przytrzymaj i mów`
  - komunikaty przerwań audio i utraty urządzenia wejściowego
  - odsłuch
  - usunięcie
  - upload
  - ustawienia audio z wyborem urządzenia wejściowego i wyjściowego
  - domyślna prędkość odtwarzania i zapamiętywanie ostatniej prędkości
  - skróty klawiaturowe sekcji aplikacji
- do domknięcia pozostały tryby nagrywania

## Etap 6 - Push, MSIX i release candidate
Zakres:
- push, jesli zatwierdzone dla `1.0`
- pipeline `MSIX`
- submission prep
- QA i hardening

Wyjscie:
- wersja release candidate

## Zasada kolejnosci
Nie zaczynamy kodowania funkcji wysokiego ryzyka bez:
- decyzji technologicznej
- minimalnego spike'u
- kryteriow akceptacji dostepnosci

## Powiazane dokumenty
- [Plan i backlog wdrozenia](windows-plan-and-backlog.md)
- [Architektura](architecture.md)
- [Wymagania dostepnosci](accessibility-requirements.md)
- [Otwarte pytania](open-questions.md)
