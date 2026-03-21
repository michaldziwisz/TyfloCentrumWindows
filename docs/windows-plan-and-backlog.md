# TyfloCentrum Windows

## Cel
- Zbudować aplikację desktopową dla Windows o funkcjonalności równoważnej obecnemu Tyflocentrum na iOS.
- Priorytetem jest dostępność dla czytników ekranu, obsługa klawiaturą i zgodność z dystrybucją przez Microsoft Store.
- Docelowy artefakt wdrożeniowy: `MSIX`, publikowany w Microsoft Store.

## Założenia i ograniczenia
- Wersja Windows ma zachować tę samą funkcjonalność użytkową, ale nie musi kopiować 1:1 interakcji specyficznych dla iOS.
- Obowiązkowe środowiska asystujące dla `1.0`: `Narrator` i `NVDA`.
- `JAWS` traktujemy jako test rozszerzony, jeśli będzie dostępny.
- Dla publikacji Store-first plan zakłada pakietowaną aplikację `MSIX` od początku projektu.
- Funkcje iOS bez sensownego odpowiednika systemowego na Windows dostają natywne odpowiedniki:
  - `Magic Tap` -> skróty klawiaturowe i jawne akcje UI,
  - `AirPlay` -> systemowy wybór urządzenia audio,
  - tryb ucha -> opcjonalnie poza zakresem `1.0`, chyba że spike wykaże niski koszt wdrożenia.

## Rekomendowany stack
- UI: `WinUI 3`
- Platforma: `Windows App SDK`
- Runtime: `.NET 8`
- Architektura: `MVVM`
- Toolkit: `CommunityToolkit.Mvvm`
- HTTP i JSON: `HttpClient`, `System.Text.Json`
- DI i logging: `Microsoft.Extensions.*`
- Odtwarzanie audio: `MediaPlayer`, `MediaPlayerElement`, `SystemMediaTransportControls`
- HTML: `WebView2` w kontrolowanym trybie lub renderer HTML -> XAML po spike'u
- Persistencja ustawień: `ApplicationData.LocalSettings`
- Persistencja danych lokalnych: JSON w `ApplicationData.LocalFolder`
- Testy jednostkowe: `xUnit`
- UI automation: `FlaUI` lub `Appium Windows Driver`
- Audyt dostępności: `Accessibility Insights for Windows`
- CI: GitHub Actions z buildem `MSIX`

## Proponowana struktura solution
- `src/Tyflocentrum.Windows.App`
- `src/Tyflocentrum.Windows.UI`
- `src/Tyflocentrum.Windows.Domain`
- `src/Tyflocentrum.Windows.Infrastructure`
- `tests/Tyflocentrum.Windows.Tests`
- `tests/Tyflocentrum.Windows.UITests`
- `docs/`

## Zakres funkcjonalny `1.0`
- Nowości: wspólny feed podcastów i artykułów
- Podcasty: kategorie, lista, szczegóły, komentarze
- Artykuły: kategorie, lista, szczegóły, strony TyfloŚwiata
- Szukaj
- Ulubione
- Ustawienia
- Odtwarzacz audio:
  - play/pause
  - seek `+-30s`
  - slider pozycji
  - prędkość do `3.0x`
  - wznawianie
  - znaczniki czasu
  - linki powiązane
- Tyfloradio:
  - live stream
  - ramówka
  - kontakt tekstowy
  - kontakt głosowy
- Pakowanie `MSIX`
- Microsoft Store release flow

## Poza zakresem pierwszego MVP
- Pełne wsparcie dla wszystkich screen readerów poza `Narrator` i `NVDA`
- Zaawansowana telemetria produktowa
- Offline mode z cache treści do czytania offline
- Rozbudowany system synchronizacji między urządzeniami

## Milestone'y

### M0. Discovery i spike'i platformowe
Cel:
- Zamknąć największe ryzyka techniczne przed rozpoczęciem produkcyjnego developmentu.

Wyjścia:
- działający spike `WinUI 3`
- spike artykułu HTML
- spike playera audio
- spike nagrywania głosówki
- decyzja `push w 1.0` albo `push w 1.1`

Kryterium wyjścia:
- `Narrator` i `NVDA` poprawnie odczytują shell, prosty formularz, player i treść HTML w wariancie wybranym do wdrożenia.

### M1. Foundation i shell aplikacji
Cel:
- Postawić produkcyjny szkielet aplikacji i standardy dostępności.

Wyjścia:
- solution, DI, logging, konfiguracja, shell nawigacyjny, style, baza testów

### M2. Read-only content MVP
Cel:
- Dowieźć wszystkie scenariusze odczytu treści bez głosówek i push.

Wyjścia:
- nowości, podcasty, artykuły, wyszukiwarka, szczegóły, komentarze, ulubione, player, radio live, ramówka

### M3. Interakcje i parity
Cel:
- Domknąć kontakt z radiem, głosówki, ustawienia i zachowania systemowe.

Wyjścia:
- formularz kontaktowy tekstowy
- ekran głosówek
- upload audio
- wznawianie playera
- skróty klawiaturowe

### M4. Release candidate
Cel:
- Zamknąć jakość, pakowanie Store i testy regresji.

Wyjścia:
- pipeline `MSIX`
- manifest Store
- checklisty a11y
- release notes

## Backlog epików

### EPIC 0. Repo i standardy projektu
Cel:
- Przygotować repozytorium, rozwiązanie i standard pracy.

Stories:
- `WIN-001` Utworzyć solution `.NET 8` z projektami `App`, `UI`, `Domain`, `Infrastructure`, `Tests`.
- `WIN-002` Skonfigurować `WinUI 3` jako packaged app w modelu single-project `MSIX`.
- `WIN-003` Dodać `EditorConfig`, analyzers, nullable, warnings-as-errors dla kodu aplikacyjnego.
- `WIN-004` Dodać podstawowy pipeline CI: restore, build, test, package.
- `WIN-005` Przygotować konwencje nazewnicze dla `AutomationId`, `AccessibleName`, focus order i skrótów.

Akceptacja:
- projekt buduje się lokalnie i w CI
- powstaje paczka `MSIX`
- nowe widoki mają szablon zgodny z wytycznymi a11y

### EPIC 1. Fundament dostępności
Cel:
- Wbudować dostępność w architekturę zamiast poprawiać ją na końcu.

Stories:
- `WIN-006` Zdefiniować standard mapowania `AutomationProperties.Name`, `HelpText`, `LocalizedControlType`.
- `WIN-007` Zdefiniować wzorzec focus management dla nawigacji, dialogów i ekranów ładowania.
- `WIN-008` Przygotować helpery do ogłoszeń statusowych dla screen readerów.
- `WIN-009` Przygotować checklistę manualnych testów `Narrator` i `NVDA`.
- `WIN-010` Dodać do CI półautomatyczny audyt `Accessibility Insights FastPass`, jeśli integracja będzie stabilna.

Akceptacja:
- istnieje jeden wspólny standard a11y dla wszystkich widoków
- każdy ekran ma jawne kryteria odczytu, fokusu i klawiatury

### EPIC 2. Shell, nawigacja i ustawienia ogólne
Cel:
- Dostarczyć główny szkielet aplikacji.

Stories:
- `WIN-011` Zaimplementować główny shell z sekcjami:
  - Nowości
  - Podcasty
  - Artykuły
  - Szukaj
  - Tyfloradio
- `WIN-012` Dodać app menu z wejściem do Ulubionych i Ustawień.
- `WIN-013` Dodać standard stron: tytuł, pasek poleceń, focus on page load.
- `WIN-014` Zaimplementować ekran Ustawień z przełącznikami zgodnymi z iOS.
- `WIN-015` Dodać skróty klawiaturowe dla głównych akcji nawigacyjnych i odtwarzacza.

Akceptacja:
- użytkownik może przejść przez cały shell klawiaturą
- `Narrator` i `NVDA` poprawnie odczytują nazwy sekcji oraz aktywny stan nawigacji

### EPIC 3. Warstwa domenowa i API
Cel:
- Przenieść kontrakty danych i logikę sieciową z iOS do .NET.

Stories:
- `WIN-016` Odtworzyć modele domenowe:
  - post
  - summary
  - category
  - comment
  - favorite
  - schedule
  - contact response
- `WIN-017` Zaimplementować klienta WordPress API dla `Tyflopodcast` i `Tyfloświat`.
- `WIN-018` Zaimplementować klienta panelu kontaktowego `kontakt.tyflopodcast.net`.
- `WIN-019` Dodać retry, timeouty i obsługę błędów zgodną z obecnym zachowaniem iOS.
- `WIN-020` Dodać warstwę cache:
  - cache protokołowy
  - krótki memory cache dla odpowiedzi `no-store`
- `WIN-021` Dodać testy kontraktowe i fixture’y odpowiedzi API.

Akceptacja:
- wszystkie endpointy z iOS mają odpowiednik w Windows
- testy jednostkowe pokrywają serializację i błędy sieciowe

### EPIC 4. Nowości
Cel:
- Dostarczyć wspólny feed najnowszych podcastów i artykułów.

Stories:
- `WIN-022` Zaimplementować widok Nowości z mieszanym feedem.
- `WIN-023` Dodać doładowywanie starszych treści.
- `WIN-024` Dodać stany:
  - loading
  - empty
  - error
  - retry
- `WIN-025` Dodać focus retention po odświeżeniu i po powrocie ze szczegółów.

Akceptacja:
- feed jest czytelny liniowo dla screen readerów
- po odświeżeniu focus nie wraca na początek bez potrzeby

### EPIC 5. Podcasty i Artykuły
Cel:
- Dostarczyć przeglądanie kategorii, list i szczegółów.

Stories:
- `WIN-026` Zaimplementować listy kategorii podcastów.
- `WIN-027` Zaimplementować listy kategorii artykułów.
- `WIN-028` Zaimplementować listy wpisów w kategorii z paginacją.
- `WIN-029` Zaimplementować szczegóły podcastu.
- `WIN-030` Zaimplementować szczegóły artykułu.
- `WIN-031` Zaimplementować przegląd stron TyfloŚwiata i numerów czasopisma.
- `WIN-032` Dodać akcje:
  - udostępnij link
  - kopiuj link
  - dodaj/usuń ulubione

Akceptacja:
- tytuł, data i typ treści są odczytywane w przewidywalnej kolejności
- każda pozycja listy ma nazwę, wartość i zestaw dostępnych akcji

### EPIC 6. Renderowanie HTML i bezpieczeństwo treści
Cel:
- Bezpiecznie i dostępnie renderować HTML artykułów oraz komentarzy.

Stories:
- `WIN-033` Wykonać spike `WebView2` vs renderer HTML -> XAML.
- `WIN-034` Zaimplementować wybrany renderer.
- `WIN-035` Ograniczyć skrypty, nawigację i schematy URL do bezpiecznego zakresu.
- `WIN-036` Dodać obsługę linków zewnętrznych.
- `WIN-037` Zweryfikować odczyt treści przez `Narrator` i `NVDA` dla:
  - krótkiego artykułu
  - długiego artykułu
  - komentarza
  - tabeli
  - listy linków

Akceptacja:
- użytkownik może czytać artykuł bez utraty fokusu i bez pułapek w osadzonym widoku
- zewnętrzne linki otwierają się bezpiecznie

### EPIC 7. Wyszukiwanie
Cel:
- Odtworzyć wyszukiwarkę z filtrowaniem zakresu i wynikami mieszanymi.

Stories:
- `WIN-038` Zaimplementować ekran Szukaj z zakresem:
  - podcasty
  - artykuły
  - wszystko
- `WIN-039` Dodać sortowanie wyników według trafności i daty.
- `WIN-040` Dodać komunikaty statusowe dla liczby wyników i błędów.
- `WIN-041` Zapewnić pełną obsługę Enter i klawiatury bez potrzeby myszy.

Akceptacja:
- po wykonaniu wyszukiwania screen reader otrzymuje komunikat o wyniku
- fokus zostaje w logicznym miejscu

### EPIC 8. Odtwarzacz audio i dodatki do odcinków
Cel:
- Dostarczyć pełny, dostępny odtwarzacz dla podcastów i live streamu.

Stories:
- `WIN-042` Zaimplementować play/pause.
- `WIN-043` Zaimplementować seek `+-30s`.
- `WIN-044` Zaimplementować slider pozycji z poprawnym odczytem czasu.
- `WIN-045` Zaimplementować zmianę prędkości do `3.0x`.
- `WIN-046` Zaimplementować wznawianie pozycji.
- `WIN-047` Zaimplementować integrację z `SystemMediaTransportControls`.
- `WIN-048` Zaimplementować pobieranie i parsowanie znaczników czasu oraz linków.
- `WIN-049` Zaimplementować widoki znaczników czasu i linków powiązanych.
- `WIN-050` Dodać komunikaty statusowe dla:
  - odtwarzania
  - pauzy
  - zmiany prędkości
  - zmiany pozycji

Akceptacja:
- cały player jest obsługiwalny z klawiatury
- `Narrator` i `NVDA` odczytują stan i wartość głównych kontrolek

### EPIC 9. Komentarze i dodatki społecznościowe
Cel:
- Dostarczyć komentarze do podcastów i ich szczegóły.

Stories:
- `WIN-051` Zaimplementować listę komentarzy.
- `WIN-052` Zaimplementować szczegóły komentarza.
- `WIN-053` Zaimplementować licznik komentarzy w szczegółach podcastu.
- `WIN-054` Dodać retry i stany błędu.

Akceptacja:
- komentarze są dostępne liniowo i logicznie
- licznik komentarzy jest aktualizowany bez gubienia fokusu

### EPIC 10. Ulubione i dane lokalne
Cel:
- Odtworzyć cały moduł ulubionych oraz lokalnych preferencji.

Stories:
- `WIN-055` Zaimplementować przechowywanie ulubionych jako JSON.
- `WIN-056` Dodać filtry ulubionych:
  - wszystko
  - podcasty
  - artykuły
  - tematy
  - linki
- `WIN-057` Dodać akcje kontekstowe:
  - odtwórz od tego miejsca
  - usuń z ulubionych
  - otwórz link
- `WIN-058` Zaimplementować ustawienia:
  - pozycja etykiety typu treści
  - tryb zapamiętywania prędkości

Akceptacja:
- dane lokalne przetrwają restart aplikacji
- moduł działa bez Core Data i bez zewnętrznej bazy

### EPIC 11. Tyfloradio i kontakt tekstowy
Cel:
- Dostarczyć scenariusze live i kontakt z radiem bez audio uploadu.

Stories:
- `WIN-059` Zaimplementować ekran Tyfloradio.
- `WIN-060` Zaimplementować live stream.
- `WIN-061` Zaimplementować ramówkę.
- `WIN-062` Zaimplementować sprawdzanie dostępności audycji interaktywnej.
- `WIN-063` Zaimplementować formularz kontaktu tekstowego.
- `WIN-064` Dodać walidację formularza i komunikaty błędów.

Akceptacja:
- kontakt tekstowy działa wyłącznie, gdy audycja jest dostępna
- błędy formularza są czytane przez screen reader

### EPIC 12. Głosówki
Cel:
- Odtworzyć kontakt głosowy z zachowaniem kluczowych funkcji i jakości UX.

Stories:
- `WIN-065` Wykonać spike nagrywania `m4a/aac` na Windows.
- `WIN-066` Zaimplementować ekran nagrania z polem imienia.
- `WIN-067` Zaimplementować start/stop nagrania z klawiatury i jawnych przycisków.
- `WIN-068` Zaimplementować odsłuch nagrania.
- `WIN-069` Zaimplementować usuwanie nagrania.
- `WIN-070` Zaimplementować append lub, jeśli spike wykaże wysoki koszt, zaakceptowany zamiennik produktowy.
- `WIN-071` Zaimplementować upload `multipart/form-data` do `ac=addvoice`.
- `WIN-072` Dodać limity:
  - długość do `20 min`
  - rozmiar zgodny z limitem backendu
- `WIN-073` Dodać obsługę przerwań audio i utraty urządzenia wejściowego.

Akceptacja:
- użytkownik może nagrać, odsłuchać i wysłać głosówkę bez użycia myszy
- błędy mikrofonu i uploadu są komunikowane czytelnie

### EPIC 13. Powiadomienia push
Cel:
- Dodać natywne powiadomienia Windows, jeśli zostaną potwierdzone dla `1.0`.

Stories:
- `WIN-074` Zdecydować, czy push wchodzi do `1.0` czy `1.1`.
- `WIN-075` Zaprojektować kontrakt tokena Windows w push-service.
- `WIN-076` Zaimplementować rejestrację tokena i preferencji.
- `WIN-077` Zaimplementować deep linki z powiadomień.
- `WIN-078` Dodać UI preferencji powiadomień.

Akceptacja:
- kliknięcie powiadomienia otwiera właściwy ekran
- użytkownik może zarządzać kategoriami powiadomień

### EPIC 14. Pakowanie, Store i release engineering
Cel:
- Przygotować aplikację do publikacji w Microsoft Store.

Stories:
- `WIN-079` Skonfigurować pełne metadane aplikacji:
  - nazwa
  - identyfikatory
  - ikony
  - capabilities
  - privacy links
- `WIN-080` Dodać build `MSIX` dla `x64`.
- `WIN-081` Dodać build `MSIX` dla `arm64`.
- `WIN-082` Przygotować pipeline release z artefaktem gotowym do submission.
- `WIN-083` Zweryfikować wymagania Store dla packaged desktop app.
- `WIN-084` Przygotować checklistę submission:
  - opis
  - screenshoty
  - polityka prywatności
  - notatki testowe

Akceptacja:
- pipeline produkuje poprawne paczki
- aplikacja przechodzi walidację submission package

### EPIC 15. QA, testy i hardening
Cel:
- Zapewnić jakość funkcjonalną i dostępnościową przed wydaniem.

Stories:
- `WIN-085` Dodać testy jednostkowe klientów API i modeli.
- `WIN-086` Dodać testy ViewModeli.
- `WIN-087` Dodać smoke testy UI dla głównych ścieżek.
- `WIN-088` Dodać testy regresji dostępności dla shella, list, formularzy i playera.
- `WIN-089` Przygotować scenariusze manualne dla:
  - `Narrator`
  - `NVDA`
  - klawiatura only
  - high contrast
  - skalowanie tekstu
- `WIN-090` Dodać diagnostykę błędów i logowanie techniczne.

Akceptacja:
- istnieje komplet testów smoke przed release
- istnieje checklista manualna dla QA i dostępności

## Sugerowana kolejność realizacji
1. `EPIC 0`
2. `EPIC 1`
3. `EPIC 2`
4. `EPIC 3`
5. `EPIC 6`
6. `EPIC 4`
7. `EPIC 5`
8. `EPIC 7`
9. `EPIC 8`
10. `EPIC 9`
11. `EPIC 10`
12. `EPIC 11`
13. `EPIC 12`
14. `EPIC 13`
15. `EPIC 14`
16. `EPIC 15`

## Proponowany plan sprintów

### Sprint 0
- `WIN-001` - `WIN-010`
- spike'i platformowe
- decyzja o HTML rendererze
- decyzja o głosówkach
- decyzja o push `1.0` vs `1.1`

### Sprint 1
- `WIN-011` - `WIN-021`
- shell, API, ustawienia, standardy a11y

### Sprint 2
- `WIN-022` - `WIN-041`
- nowości, kategorie, listy, wyszukiwarka

### Sprint 3
- `WIN-042` - `WIN-058`
- player, komentarze, ulubione, persistencja

### Sprint 4
- `WIN-059` - `WIN-073`
- Tyfloradio, kontakt tekstowy, głosówki

### Sprint 5
- `WIN-074` - `WIN-090`
- push, `MSIX`, Store, QA, hardening

## Kryteria akceptacji dostępności dla całego produktu
- Każdy interaktywny element ma poprawną nazwę dostępną przez `UI Automation`.
- Cała aplikacja jest obsługiwalna bez myszy.
- Focus order jest zgodny z układem wizualnym i logiką ekranu.
- Widoki ładowania, błędów i sukcesu są czytane przez screen reader bez potrzeby ręcznego szukania komunikatu.
- Dialogi przejmują focus przy otwarciu i oddają go po zamknięciu.
- Listy i szczegóły nie gubią fokusu po asynchronicznych aktualizacjach.
- Odtwarzacz zgłasza:
  - stan odtwarzania
  - pozycję
  - prędkość
  - dostępność seek
- Formularze mają etykiety, walidację i czytelne komunikaty błędów.
- Aplikacja działa poprawnie w:
  - `Narrator`
  - `NVDA`
  - wysokim kontraście
  - skali tekstu powyżej domyślnej

## Definition of Done
- Kod ma testy adekwatne do ryzyka zmiany.
- Widok przeszedł manualny test klawiatury.
- Widok przeszedł test `Narrator` i `NVDA`.
- Wszystkie nowe kontrolki mają ustawione właściwości automatyzacji.
- Błędy, puste stany i loading mają poprawny odczyt.
- Zmiana została dodana do dokumentacji, jeśli wpływa na architekturę lub proces release.

## Główne ryzyka
- `WebView2` może dawać gorszy UX czytania niż renderer natywny.
- Głosówki mogą wymagać osobnego spike'u technologicznego przed obietnicą pełnego parity.
- Push Windows może narzucić ograniczenia wdrożeniowe zależne od modelu pakowania.
- Wsparcie `Windows 10` może zwiększyć koszt testów i ilość workaroundów.

## Decyzje do zamknięcia przed startem developmentu
- Czy wspieramy `Windows 10` czy tylko `Windows 11`.
- Czy push jest częścią `1.0`.
- Czy `JAWS` jest wymagany na poziomie release criteria.
- Czy tryb ucha jest wymagany, czy może być zastąpiony przez prostszy model desktopowy.
