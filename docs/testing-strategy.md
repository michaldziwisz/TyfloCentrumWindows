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
  - aplikacja uruchamia się w oknie zmaksymalizowanym
  - `Alt+1` do `Alt+8` przełącza sekcje bez użycia myszy
  - strzałka w górę i strzałka w dół przełącza aktywną sekcję na liście sekcji
  - wpisanie pierwszej litery sekcji przenosi fokus do kolejnej sekcji zaczynającej się od tej litery
  - `Enter` na liście sekcji przechodzi do głównej kontroli bieżącej sekcji, jak `Tab`
  - po przełączeniu focus trafia na aktywną pozycję listy sekcji
  - skrót sekcji jest widoczny bezpośrednio przy nazwie każdej sekcji na liście, a nie w osobnym komunikacie pomocniczym
- `Narrator`:
  - odczytuje sekcje jako zwykłe elementy, bez technicznego słowa `lista`, bez komunikatów o drzewie i poziomach
  - odczytuje komunikat o aktualnie wybranej sekcji po użyciu skrótu
- `NVDA`:
  - odczytuje nazwy sekcji bez technicznych identyfikatorów i bez semantyki drzewa
  - nie zatrzymuje się osobno na samym skrócie `Alt+1`, `Alt+2` i tak dalej jako oddzielnym statycznym tekście

### Modul `Nowosci`
- klawiatura:
  - focus wchodzi do listy nowości i menu kontekstowego w logicznej kolejności
  - `Enter` uruchamia domyślną akcję wiersza
  - wpisanie pierwszej litery tytułu przenosi fokus do kolejnej nowości zaczynającej się od tej litery
  - `Ctrl+C` kopiuje adres strony podcastu albo artykułu dla zaznaczonego elementu
  - `Ctrl+P` kopiuje adres pliku podcastu dla zaznaczonego podcastu
  - `Escape` z listy nowości przenosi fokus do listy sekcji głównych
- `Narrator`:
  - odczytuje nazwe sekcji
  - odczytuje element jako tytuł, datę i zajawkę, a typ treści tylko zgodnie z ustawieniem dostępności
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
  - szybkie wielokrotne dojście do końca listy, na przykład kilkukrotne `End`, nie zamyka aplikacji i nie uruchamia równoległych awaryjnych doładowań
  - po automatycznym doczytaniu starszych treści fokus i zaznaczenie zostają na tym samym wpisie, bez przeskoku na inny element listy
  - pozostawienie sekcji `Nowości` otwartej przez kilka minut powoduje automatyczne pojawienie się nowo opublikowanego podcastu albo artykułu bez restartu aplikacji

### Modul `Kategorie podcastów i artykułów`
- klawiatura:
  - strzałka w górę i strzałka w dół przełącza kategorię bez dodatkowego `Enter`
  - wpisanie pierwszej litery kategorii przenosi fokus do kolejnej kategorii zaczynającej się od tej litery
  - wpisanie pierwszej litery tytułu na liście `Podcastów` albo `Artykułów` przenosi fokus do kolejnego wpisu zaczynającego się od tej litery
  - po przełączeniu kategorii fokus zostaje na nowo wybranej kategorii, a nie wraca na pierwszy element listy
- `Narrator`:
  - odczytuje nowo wybraną kategorię tylko raz, bez nieoczekiwanego powrotu na `Aktualności`
- `NVDA`:
  - odczytuje zmianę kategorii i nie gubi fokusu podczas przeładowania listy treści
- funkcjonalnie:
  - zmiana kategorii przeładowuje listę treści dla wybranej kategorii
  - po przeładowaniu lista kategorii zachowuje zaznaczenie i fokus na aktywnej pozycji
  - w listach `Podcastów` i `Artykułów` menu kontekstowe pokazuje skróty `Ctrl+S`, `Ctrl+U`, `Ctrl+C`, a dla podcastów także `Ctrl+P`
  - po automatycznym doczytaniu kolejnych pozycji w `Podcastach` albo `Artykułach` fokus i zaznaczenie zostają na tym samym wpisie
  - pozostawienie sekcji `Podcastów` albo `Artykułów` otwartej przez kilka minut powoduje automatyczne pojawienie się nowo opublikowanej treści bez restartu aplikacji

### Modul `Czytanie artykulu`
- klawiatura:
  - focus po otwarciu przechodzi od razu do treści artykułu
  - `Tab` porusza się już tylko po zawartości osadzonego czytnika artykułu, bez dodatkowych przycisków `Odśwież`, `Otwórz w przeglądarce` i bez przycisku `Zamknij` w cyklu tabulacji
  - `Escape` zamyka widok czytania i wraca do listy źródłowej
- `Narrator`:
  - odczytuje nagłówek artykułu i treść bez przechodzenia przez elementy serwisu WordPress
- `NVDA`:
  - odczytuje treść jako jeden spójny dokument, bez technicznego chrome przeglądarki
  - przy wyłączonym trybie przeglądania po wejściu do treści ogłasza krótką nazwę hosta `Czytnik artykułu`, bez dodatkowego dublowania nazwy z warstwy HTML
- funkcjonalnie:
  - widok czytania renderuje tylko treść artykułu z API wraz z metadanymi
  - linki z treści artykułu otwierają się w zewnętrznej przeglądarce bez opuszczania widoku czytania

### Modul `Tyfloradio`
- klawiatura:
  - przyciski `Skontaktuj się z Tyfloradiem` i `Nagraj głosówkę` pozostają osiągalne z klawiatury niezależnie od statusu audycji
  - `Tab` pozwala dojść do focusowalnego regionu `Status audycji interaktywnej` i do przycisku `Pokaż ramówkę`
- `Narrator`:
  - odczytuje pełny komunikat błędu przy próbie otwarcia kontaktu tekstowego albo głosówki poza audycją interaktywną
  - odczytuje aktualny status audycji interaktywnej oraz przycisk `Pokaż ramówkę Tyfloradia`
- `NVDA`:
  - odczytuje tytuł i treść `InfoBar`, a nie samo techniczne `Ikona błędu`
- funkcjonalnie:
  - zamknięcie formularza kontaktu bez wysyłki nie pokazuje błędu
  - rzeczywisty problem z otwarciem formularza kontaktu tekstowego albo głosowego pokazuje czytelny komunikat
  - bieżący status Tyfloradia jest dostępny z klawiatury, a przycisk `Pokaż ramówkę Tyfloradia` odsłania w tej samej sekcji wielowierszowe pole tylko do odczytu i przenosi do niego fokus
  - przy braku opublikowanej ramówki przycisk `Pokaż ramówkę Tyfloradia` odsłania pole z komunikatem `Brak dostępnej ramówki.`

### Modul `Ustawienia`
- klawiatura:
  - focus przechodzi przez przyciski akcji, pola wyboru i przełącznik w logicznej kolejności
  - `Escape` wraca do listy sekcji głównych
- `Narrator`:
  - odczytuje nagłówki sekcji, etykiety pól `Urządzenie wejściowe`, `Urządzenie wyjściowe`, `Domyślna prędkość odtwarzania`
  - odczytuje zmianę stanu przełącznika zapamiętywania prędkości
  - odczytuje zmianę stanu przełącznika zapamiętywania głośności
  - odczytuje pole `Typ treści na listach` i wybraną opcję bez dodatkowych technicznych nazw
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
  - zapamiętana głośność odtwarzania jest współdzielona między podcastami i `Tyfloradiem`, jeśli opcja jest włączona
  - ustawienie `Typ treści na listach` pozwala wybrać brak typu, typ przed nazwą albo typ po nazwie dla nowości, podcastów, artykułów, wyników wyszukiwania i pozycji spisu treści `TyfloŚwiata`

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
  - `Enter` na liście sekcji przechodzi najpierw do pola `Typ zgłoszenia`, bez pomijania wyboru kategorii
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
  - pole `Opis publiczny` działa jako natywne wielowierszowe pole edycji `RichEditBox`
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
  - odczytuje pozycję spisu treści bez pustych przycisków wewnątrz wiersza i z typem treści tylko zgodnie z ustawieniem dostępności
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
  - focus przechodzi przez widoczne przyciski `Wstecz 30 s`, `Odtwarzaj` albo `Pauza`, `Dalej 30 s`, `Przesyłaj do urządzenia`, suwak pozycji, wybór prędkości, `Głośność`, przyciski dodatków i akcje w listach w logicznej kolejności
  - fokus na przyciskach przewijania, przycisku odtwarzania i suwaku pozycji jest zawsze czytelny wizualnie, bez znikania kontrolek po samym wejściu z klawiatury
  - `Ctrl+spacja` wstrzymuje i wznawia odtwarzanie
  - `Ctrl+strzałka w lewo` i `Ctrl+strzałka w prawo` przewijają podcast o `30 s`
  - `Alt+strzałka w górę` i `Alt+strzałka w dół` zmieniają prędkość podcastu
  - `Ctrl+strzałka w górę` i `Ctrl+strzałka w dół` zmieniają głośność odtwarzacza
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
  - przyciski transportu i suwak pozycji są jawnie widoczne jako osobny pasek sterowania także przy nawigacji samą klawiaturą
  - przycisk `Przesyłaj do urządzenia` otwiera wybór urządzenia zewnętrznego dla podcastu i `Tyfloradia`, a po połączeniu pozwala też rozłączyć przesyłanie
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
  - wpisanie pierwszej litery tytułu przenosi fokus do kolejnego pasującego wpisu także w `Szukaj`
  - w `Artykułach` strzałki i litery działają także na pozycji `TyfloŚwiat` w tej samej liście po lewej
- `Narrator`:
  - odczytuje komunikat `Dodano do ulubionych` albo `Usunięto z ulubionych` po `Ctrl+D`
  - odczytuje listy bez technicznego słowa `lista`, a typ treści tylko zgodnie z ustawieniem dostępności
- funkcjonalnie:
  - listy nie pokazują już osobnego, stałego wiersza `Podcast` albo `Artykuł` nad tytułem
  - menu kontekstowe podcastu pokazuje `Pokaż komentarze`, `Pokaż znaczniki czasu` i `Pokaż odnośniki` tylko wtedy, gdy odcinek naprawdę ma takie dodatki
  - `Pokaż znaczniki czasu` otwiera listę samych znaczników bez wchodzenia do playera
  - lista samych znaczników ma takie samo menu kontekstowe jak w playerze, w tym `Przejdź` i `Dodaj do ulubionych`
  - `Enter` na znaczniku czasu uruchamia player podcastu od wybranej czasówki
  - `Pokaż odnośniki` otwiera listę samych odnośników bez wchodzenia do playera
  - lista samych odnośników ma takie samo menu kontekstowe jak w playerze, w tym `Otwórz`, `Kopiuj`, `Udostępnij` i `Dodaj do ulubionych`
  - `Ctrl+D` i `Ctrl+U` działają w liście samych odnośników tak samo jak w playerze
  - `Enter` na odnośniku otwiera go w przeglądarce
  - `Pokaż komentarze` otwiera listę komentarzy bez wchodzenia do playera
  - szybkie wielokrotne dojście do końca listy w `Podcastach` albo `Artykułach`, na przykład kilkukrotne `End`, nie zamyka aplikacji i nie powoduje równoległego dublowania doładowań

### Modul `Komentarze podcastu`
- klawiatura:
  - po otwarciu podcastu można pokazać komentarze bez opuszczania odtwarzacza
  - komentarze można też otworzyć bezpośrednio z menu kontekstowego podcastu
  - focus przechodzi przez licznik komentarzy, listę komentarzy i przycisk `Szczegóły komentarza` w logicznej kolejności
  - `Enter` na zaznaczonym komentarzu rozwija albo zwija jego pełną treść
- `Narrator`:
  - odczytuje licznik komentarzy po załadowaniu odtwarzacza podcastu
  - odczytuje przycisk `Szczegóły komentarza` z nazwą autora
- `NVDA`:
  - odczytuje element listy komentarzy bez technicznych nazw i z sensownym skrótem treści
- funkcjonalnie:
  - licznik komentarzy aktualizuje się po załadowaniu podcastu bez gubienia fokusu
  - pełna treść komentarza rozwija się inline w odtwarzaczu bez otwierania zagnieżdżonego dialogu
  - komentarze główne są sortowane od najstarszych do najnowszych, a odpowiedzi pojawiają się bezpośrednio pod rodzicem
  - odpowiedzi są także wizualnie odróżnione od komentarzy głównych przez wcięcie i lewy akcent

## Dokumenty powiazane
- [Wymagania dostepnosci](accessibility-requirements.md)
- [Integracje API](api-integrations.md)
- [Roadmapa implementacji](implementation-roadmap.md)
