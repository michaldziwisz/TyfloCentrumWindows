# Strategia testow

## Cel
Zapewnic wysoka pewnosc poprawnosci funkcjonalnej i dostepnosciowej przy rozsadnym koszcie utrzymania testow.

## Warstwy testow

### Testy jednostkowe
Zakres:
- modele domenowe
- parsery
- polityki wyszukiwania
- polityki odtwarzania
- mapowanie odpowiedzi API
- persistencja lokalna

Cel:
- szybki feedback
- wysoka powtarzalnosc

### Testy ViewModeli
Zakres:
- logika ekranow
- przejscia stanow
- obsluga bledow
- focus intentions i komunikaty stanu na poziomie logiki

Cel:
- sprawdzic zachowanie UI bez uruchamiania prawdziwego okna

### Testy kontraktowe API
Zakres:
- deserializacja odpowiedzi
- paginacja
- obsluga naglowkow `X-WP-Total` i `X-WP-TotalPages`
- obsluga bledow i timeoutow
- budowanie `multipart/form-data` dla glosowek

Cel:
- ograniczyc regresje po zmianach po stronie serwera lub klienta

### Smoke testy UI
Zakres:
- uruchomienie aplikacji
- przejscie przez glowne sekcje
- otwarcie playera
- otwarcie artykulu
- wyszukiwanie
- formularz kontaktu
- formularz glosowki

Cel:
- szybko wykryc regresje krytycznych sciezek

### Testy manualne dostepnosci
Zakres:
- `Narrator`
- `NVDA`
- klawiatura only
- wysoki kontrast
- zwiekszony tekst

Cel:
- potwierdzic realne zachowanie aplikacji z technologiami wspomagajacymi

## Dane testowe
- fixture'y odpowiedzi HTTP przechowywane lokalnie w testach
- kontrolowane stuby dla scenariuszy:
  - error
  - timeout
  - empty
  - malformed payload
- osobny fixture dla:
  - podcastu z komentarzami
  - artykulu z HTML
  - odcinka ze znacznikami czasu
  - aktywnej audycji radiowej
  - odpowiedzi `addvoice`

## Srodowiska testowe
- lokalnie na maszynie developerskiej
- CI dla build i testow bez UI lub z ograniczonym smoke
- manualna walidacja release candidate na realnym Windows

## Bramy jakosci

### Pull request
- build przechodzi
- testy jednostkowe przechodza
- testy ViewModeli przechodza
- krytyczne testy kontraktowe przechodza

### Release candidate
- smoke testy UI przechodza
- testy manualne `Narrator` i `NVDA` sa odhaczone
- test wysokiego kontrastu przechodzi
- test `MSIX` install/uninstall przechodzi

## Priorytety automatyzacji
Najpierw automatyzowac:
- warstwe API
- ViewModel'e
- player
- formularze
- glowne sciezki shellu

Pozniej automatyzowac:
- scenariusze niszowe
- rozbudowane kombinacje ustawien

## Dodatkowa checklista dla glosowek
- `Narrator`:
  - odczytuje stan nagrywania i zmiane czasu trwania
  - odczytuje blad braku mikrofonu lub braku uprawnien
- `NVDA`:
  - odczytuje przyciski `Rozpocznij`, `Zatrzymaj`, `Odsłuchaj`, `Usuń`, `Wyślij`
- audio:
  - przy pierwszej próbie nagrania Windows pokazuje systemowe pytanie o dostęp do mikrofonu
  - po zaakceptowaniu pytania nagranie może ruszyć bez ręcznego otwierania ustawień
  - mikrofon wspierający `RAW` pozwala rozpocząć nagrywanie
  - mikrofon bez `RAW` nadal pozwala rozpocząć nagrywanie w najlepszym trybie dostępnym na urządzeniu
  - po zamknięciu dialogu plik tymczasowy jest usuwany

## Biezaca checklista manualna

### Modul `Shell`
- klawiatura:
  - `Alt+1` do `Alt+8` przełącza sekcje bez użycia myszy
  - strzałka w górę i strzałka w dół przełącza aktywną sekcję na liście sekcji
  - wpisanie pierwszej litery sekcji przenosi fokus do kolejnej sekcji zaczynającej się od tej litery
  - `Enter` na liście sekcji przechodzi do głównej kontroli bieżącej sekcji, jak `Tab`
  - po przełączeniu focus trafia na aktywną pozycję listy sekcji
- `Narrator`:
  - odczytuje sekcje jako zwykłe elementy listy, bez komunikatów o drzewie i poziomach
  - odczytuje komunikat o aktualnie wybranej sekcji po użyciu skrótu
- `NVDA`:
  - odczytuje nazwy sekcji bez technicznych identyfikatorów i bez semantyki drzewa

### Modul `Nowosci`
- klawiatura:
  - focus wchodzi do listy nowości i menu kontekstowego w logicznej kolejności
  - `Enter` uruchamia domyślną akcję wiersza
  - `Escape` z listy nowości przenosi fokus do listy sekcji głównych
- `Narrator`:
  - odczytuje nazwe sekcji
  - odczytuje element listy jako typ tresci, tytul, date i zajawke
  - odczytuje menu kontekstowe z poprawnymi nazwami akcji `Odtwórz` albo `Otwórz artykuł`, `Szczegóły`, `Otwórz w przeglądarce`
- `NVDA`:
  - odczytuje listę i menu kontekstowe bez pustych lub zdublowanych nazw
- stany:
  - ladowanie jest czytelne wizualnie
  - blad pobierania pokazuje komunikat i przycisk ponowienia
  - pusty wynik pokazuje komunikat pustego stanu
- akcja:
  - podcast uruchamia wbudowany odtwarzacz
  - artykuł otwiera się w wewnętrznej przeglądarce `WebView2`
  - `Pobierz` w menu kontekstowym zapisuje podcast jako `mp3` albo artykuł jako pojedynczy plik `html`
  - po zamknięciu widoku artykułu focus wraca na poprzedni element listy
  - dojście do końca listy automatycznie dociąga starsze treści bez ręcznego przycisku `więcej`

### Modul `Kategorie podcastów i artykułów`
- klawiatura:
  - strzałka w górę i strzałka w dół przełącza kategorię bez dodatkowego `Enter`
  - wpisanie pierwszej litery kategorii przenosi fokus do kolejnej kategorii zaczynającej się od tej litery
  - po przełączeniu kategorii fokus zostaje na nowo wybranej kategorii, a nie wraca na pierwszy element listy
- `Narrator`:
  - odczytuje nowo wybraną kategorię tylko raz, bez nieoczekiwanego powrotu na `Aktualności`
- `NVDA`:
  - odczytuje zmianę kategorii i nie gubi fokusu podczas przeładowania listy treści
- funkcjonalnie:
  - zmiana kategorii przeładowuje listę treści dla wybranej kategorii
  - po przeładowaniu lista kategorii zachowuje zaznaczenie i fokus na aktywnej pozycji

### Modul `Czytanie artykulu`
- klawiatura:
  - focus po otwarciu przechodzi od razu do treści artykułu
  - `Escape` zamyka widok czytania i wraca do listy źródłowej
- `Narrator`:
  - odczytuje nagłówek artykułu i treść bez przechodzenia przez elementy serwisu WordPress
- `NVDA`:
  - odczytuje treść jako jeden spójny dokument, bez technicznego chrome przeglądarki
- funkcjonalnie:
  - widok czytania renderuje tylko treść artykułu z API wraz z metadanymi
  - linki z treści artykułu otwierają się w zewnętrznej przeglądarce bez opuszczania widoku czytania

### Modul `Ustawienia`
- klawiatura:
  - focus przechodzi przez przyciski akcji, pola wyboru i przełącznik w logicznej kolejności
  - `Escape` wraca do listy sekcji głównych
- `Narrator`:
  - odczytuje nagłówki sekcji, etykiety pól `Urządzenie wejściowe`, `Urządzenie wyjściowe`, `Domyślna prędkość odtwarzania`
  - odczytuje zmianę stanu przełącznika zapamiętywania prędkości
  - odczytuje komunikat po zapisaniu ustawień
- `NVDA`:
  - odczytuje listy urządzeń bez pustych nazw i bez numerów identyfikatorów urządzeń
- funkcjonalnie:
  - zmiana urządzenia wyjściowego jest używana przy następnym otwarciu odtwarzacza
  - zmiana urządzenia wejściowego jest używana przy następnym nagraniu głosówki
  - wybranie folderu pobierania zapisuje ścieżkę do następnych pobrań podcastów i artykułów
  - pozostawienie pustej ścieżki oznacza użycie systemowego folderu `Pobrane`
  - przełączniki powiadomień o nowych podcastach i artykułach zapisują się niezależnie
  - po wybraniu niedostępnego wcześniej urządzenia aplikacja pokazuje czytelny komunikat i nie przechodzi na inne urządzenie po cichu
  - zapamiętana prędkość odtwarzania jest odtwarzana po ponownym otwarciu playera, jeśli opcja jest włączona

### Modul `Powiadomienia o nowych treściach`
- funkcjonalnie:
  - przy pierwszym uruchomieniu monitor zapisuje bieżący stan i nie pokazuje zaległych toastów
  - kolejne nowe podcasty pokazują toast `Nowy podcast`
  - kolejne nowe artykuły pokazują toast `Nowy artykuł`
  - wyłączenie powiadomień dla podcastów albo artykułów zatrzymuje toast dla tej kategorii, ale nie blokuje aktualizacji stanu ostatnio widzianych wpisów
  - powiadomienia działają tylko wtedy, gdy aplikacja jest uruchomiona

### Modul `Zgłoś błąd lub sugestię`
- klawiatura:
  - `Alt+8` przełącza do sekcji zgłoszeń
  - `Enter` na liście sekcji przechodzi do pola tytułu zgłoszenia
  - `Tab` przechodzi kolejno przez typ zgłoszenia, tytuł, opis, pola wyboru i przyciski akcji
  - `Escape` z formularza wraca do listy sekcji głównych
- `Narrator`:
  - odczytuje ostrzeżenie o publicznym issue
  - odczytuje etykiety pól `Typ zgłoszenia`, `Tytuł publicznego zgłoszenia`, `Opis publiczny`
  - odczytuje status wysyłki i błąd serwera
- `NVDA`:
  - odczytuje stan pól wyboru diagnostyki i prywatnego logu bez pustych nazw
  - odczytuje pojawienie się przycisku otwierającego publiczne issue po udanej wysyłce
- funkcjonalnie:
  - publiczne zgłoszenie zawiera tylko tytuł, opis i bezpieczną diagnostykę jawną
  - opcjonalny log techniczny trafia wyłącznie do prywatnego repo intake
  - klient zawsze wysyła nagłówek `User-Agent`

### Modul `TyfloŚwiat`
- klawiatura:
  - wybór `TyfloŚwiata` jest dostępny jako element listy kategorii w sekcji `Artykuły`
  - `Enter` na kategorii `TyfloŚwiat` przechodzi do listy roczników, a nie aktywuje dodatkowego okna
  - `Enter` na liście roczników przechodzi do listy numerów
  - `Enter` na liście numerów przechodzi do listy artykułów numeru albo do akcji numeru, jeśli numer nie ma spisu treści
  - `Enter` na pozycji spisu treści otwiera artykuł w tym samym readerze co zwykły artykuł portalu
  - `Escape` z listy artykułów numeru albo akcji numeru cofa fokus do listy numerów
  - kolejny `Escape` z listy numerów cofa fokus do listy roczników
  - kolejny `Escape` z listy roczników cofa fokus do listy kategorii `Artykułów`
- `Narrator`:
  - odczytuje rocznik z liczbą numerów
  - odczytuje numer czasopisma jako numer, tytuł i datę
  - odczytuje pozycję spisu treści jako zwykły artykuł bez pustych przycisków wewnątrz wiersza
- `NVDA`:
  - odczytuje listę numerów i spis treści bez nazw technicznych ani pustych kontrolek
- funkcjonalnie:
  - wybór roku filtruje listę numerów
  - wybór numeru ładuje PDF i spis treści albo treść numeru
  - przycisk PDF otwiera właściwy plik
  - szczegóły pozycji spisu treści otwierają poprawną stronę TyfloŚwiata
  - pozycję spisu treści można dodać do ulubionych bez otwierania szczegółów
  - przycisk udostępniania otwiera systemowe udostępnianie dla strony TyfloŚwiata
  - strona TyfloŚwiata może zostać dodana do ulubionych i pojawia się potem w sekcji `Ulubione`

### Modul `Ulubione`
- klawiatura:
  - focus przechodzi przez filtr, listę pozycji, przycisk otwarcia w przeglądarce i usuwanie w logicznej kolejności
  - `Enter` na pozycji wykonuje jej akcję domyślną
  - po zamknięciu szczegółów pozycji albo playera fokus wraca do tej samej pozycji listy `Ulubionych`
  - dopiero `Escape` z listy `Ulubionych` przenosi fokus do listy sekcji głównych
- `Narrator`:
  - odczytuje typ pozycji jako `Podcast`, `Artykuł`, `Artykuł TyfloŚwiata`, `Temat` albo `Odnośnik`
  - odczytuje poprawną nazwę akcji głównej dla tematu, linku i artykułu
- `NVDA`:
  - odczytuje listę bez technicznych identyfikatorów i bez pustych wierszy metadanych
- funkcjonalnie:
  - filtr `Tematy` pokazuje tylko zapisane timestampy
  - filtr `Linki` pokazuje tylko zapisane odnośniki
  - uruchomienie ulubionego tematu otwiera player na zapisanym czasie
  - uruchomienie ulubionego odnośnika otwiera właściwy adres
  - przycisk kopiowania odnośnika zapisuje właściwy adres w schowku systemowym
  - przycisk udostępniania otwiera systemowe udostępnianie dla bieżącej pozycji
  - usunięcie jednego tematu nie usuwa pozostałych tematów z tego samego podcastu

### Modul `Odtwarzacz`
- klawiatura:
  - focus przechodzi przez przyciski `Wstecz 30 s`, `Dalej 30 s`, wybór prędkości, `Głośność`, przyciski dodatków i akcje w listach w logicznej kolejności
  - `Ctrl+spacja` wstrzymuje i wznawia odtwarzanie
  - `Ctrl+strzałka w lewo` i `Ctrl+strzałka w prawo` przewijają podcast o `30 s`
  - `Ctrl+strzałka w górę` i `Ctrl+strzałka w dół` zmieniają prędkość podcastu
- `Narrator`:
  - odczytuje zmianę prędkości, zmianę głośności, przejście do znacznika czasu, dodanie tematu do ulubionych i otwarcie odnośnika
  - odczytuje komunikat `Dodano do ulubionych` albo `Usunięto z ulubionych` po `Ctrl+D` na temacie i odnośniku
  - odczytuje przyciski `Znaczniki czasu` i `Odnośniki`
  - odczytuje opis skrótów klawiaturowych odtwarzacza
- `NVDA`:
  - odczytuje listę znaczników czasu jako tytuł i czas
  - odczytuje listę odnośników jako tytuł i host bez pustych wartości
- funkcjonalnie:
  - player `Tyfloradia` pozwala zmienić głośność bez wychodzenia z dialogu
  - komentarz z sekcją `Znaczniki czasu` daje listę markerów w kolejności rosnącej
  - komentarz z sekcją `Odnośniki` albo `Linki` daje listę linków
  - kliknięcie znacznika przewija player do właściwego czasu i uruchamia odtwarzanie
  - kliknięcie odnośnika otwiera go w systemowej przeglądarce lub w domyślnym kliencie poczty dla `mailto:`
  - przycisk kopiowania zapisuje adres odnośnika do schowka systemowego
  - przycisk udostępniania otwiera systemowe udostępnianie dla wybranego odnośnika
  - temat i odnośnik można dodać oraz usunąć z ulubionych bez zamykania playera
  - po zamknięciu dialogu i ponownym otwarciu podcast wznawia się od ostatniej zapisanej pozycji
  - po dojściu odcinka do końca i ponownym otwarciu player startuje od początku, a nie od końcówki
  - skróty klawiaturowe działają także wtedy, gdy focus jest na obszarze playera, a nie na konkretnym przycisku

### Modul `Podcasty`, `Artykuły`, `Szukaj`
- klawiatura:
  - `Escape` z listy treści cofa fokus do listy kategorii albo pola wyszukiwania
  - kolejny `Escape` cofa fokus do listy sekcji głównych
  - `Ctrl+D` na zaznaczonym wpisie przełącza ulubione bez odrywania rąk od klawiatury
  - `Enter` na liście kategorii przechodzi do listy treści bez dodatkowego potwierdzania kategorii
  - `Enter` na zaznaczonym wpisie wykonuje jego akcję domyślną
  - w `Artykułach` strzałki i litery działają także na pozycji `TyfloŚwiat` w tej samej liście po lewej
- `Narrator`:
  - odczytuje komunikat `Dodano do ulubionych` albo `Usunięto z ulubionych` po `Ctrl+D`

### Modul `Komentarze podcastu`
- klawiatura:
  - focus przechodzi przez licznik komentarzy, treść skróconą i przycisk `Szczegóły komentarza` w logicznej kolejności
- `Narrator`:
  - odczytuje licznik komentarzy po załadowaniu szczegółów podcastu
  - odczytuje przycisk `Szczegóły komentarza` z nazwą autora
- `NVDA`:
  - odczytuje element listy komentarzy bez technicznych nazw i z sensownym skrótem treści
- funkcjonalnie:
  - licznik komentarzy aktualizuje się po odświeżeniu komentarzy bez gubienia fokusu
  - otwarcie szczegółów komentarza pokazuje pełną treść i autora w osobnym dialogu

## Dokumenty powiazane
- [Wymagania dostepnosci](accessibility-requirements.md)
- [Integracje API](api-integrations.md)
- [Roadmapa implementacji](implementation-roadmap.md)
