# Integracje API

## Cel
Ten dokument opisuje zewnetrzne kontrakty HTTP, ktore wersja Windows musi obsluzyc, aby osiagnac parity z obecna aplikacja iOS.

## Glowne systemy zewnetrzne
- `https://tyflopodcast.net/wp-json`
- `https://tyfloswiat.pl/wp-json`
- `https://kontakt.tyflopodcast.net/json.php`
- opcjonalnie: `https://tyflocentrum.tyflo.eu.org`
- opcjonalnie: `Sygnalista` `POST /v1/report`

## WordPress - Tyflopodcast

### Podstawowe endpointy
- `GET /wp/v2/posts`
- `GET /wp/v2/posts/{id}`
- `GET /wp/v2/pages`
- `GET /wp/v2/pages/{id}`
- `GET /wp/v2/categories`
- `GET /wp/v2/comments`
- `POST /wp-comments-post.php`

### Zachowania do odtworzenia
- paginacja przez `page` i `per_page`
- odczyt naglowkow:
  - `X-WP-Total`
  - `X-WP-TotalPages`
- filtrowanie po kategorii
- wyszukiwanie po parametrze `search`
- ograniczanie zwracanych pol przez `_fields`
- tekstowe wersje audycji:
  - link jest wykrywany w tresci wpisu po adresie z `/tekstowe-wersje-audycji/` albo po etykiecie zawierajacej slowa `tekstowa` i `wersja`
  - docelowa strona jest pobierana z `GET /wp/v2/pages/{id}` dla linkow `?page_id=...` albo z `GET /wp/v2/pages?slug=...`
- dla wysylki komentarza:
  - odczyt komentarzy pozostaje na `GET /wp/v2/comments`
  - publikacja komentarza uzywa tego samego formularza co strona WWW, bo REST `POST /wp/v2/comments` moze wymagac logowania mimo wlaczonego publicznego komentowania
  - body:
    - `comment`
    - `author`
    - `email`
    - `url`
    - `comment_post_ID`
    - `comment_parent`
    - `submit`
    - `akismet_comment_nonce` jesli formularz strony go udostepnia
    - `ak_js`
    - `ak_hp_textarea`
  - aplikacja musi obslugiwac odpowiedzi `WordPress` dla statusow:
    - redirect bez parametrow moderacji -> komentarz opublikowany
    - redirect z `unapproved` albo `moderation-hash` -> komentarz przekazany do moderacji
    - strona bledu z komunikatem o spamie -> komentarz zakwalifikowany jako spam
    - strona bledu `WordPress` -> pokazac komunikat uzytkownikowi wprost

## WordPress - TyfloSwiat

### Podstawowe endpointy
- `GET /wp/v2/posts`
- `GET /wp/v2/posts/{id}`
- `GET /wp/v2/categories`
- `GET /wp/v2/pages`
- `GET /wp/v2/pages/{id}`

### Zachowania do odtworzenia
- pobieranie stron po `slug`
- pobieranie podstron po `parent`
- osobne listy kategorii i wpisow

## Opcjonalne czasy na listach

Listy posts i pages proszą dodatkowo o `tyflocentrum` oraz opcjonalne `modified_gmt`. Nie rozszerzają zapytań o treść ani audio. Model toleruje dowolny kształt tych dodatkowych pól; błędna wartość nie unieważnia wpisu.

Długość audio pochodzi z WordPressa: schema 1, `audio_status=ready`, dodatnie skończone `duration_seconds` do 2147483647. Liczba jest zmiennoprzecinkowa, zaokrąglana w górę do sekundy. Ulubione mogą odświeżyć ją batchowym `include` z polami `id,tyflocentrum`, bez pobierania pełnego wpisu.

Czas czytania pochodzi wyłącznie z `https://tyflocentrum.tyflo.eu.org/v1/metadata?source=tyfloswiat.pl&type=posts&ids=...` (osobno `pages`). Porcje mają do 50 dodatnich unikalnych ID. `ContentTimeService` scala po kluczu źródło/typ/ID, odrzuca obce i niejednoznaczne duplikaty. Osobny timeout 3 s, do dwóch równoległych żądań, brak retry, deduplikacja aktywnych kluczy. Cache w pamięci: 512 wpisów, do 5 min dla wyniku i 1 min dla braku/awarii. Ważność nigdy nie przekracza 24 h od `checked_at`; źródłowa nowsza modyfikacja unieważnia czas. Brak modyfikacji w `context=embed` jest dozwolony. Wymagane schema 1, `freshness=fresh`, `text_status=ready`, dodatnie liczniki i `reading_minutes=ceil(word_count/200)`.

`ContentTimeEnrichment` aktualizuje istniejące wiersze już po zbudowaniu listy, niezależnie od jej stanu ładowania. Anulowanie i numer generacji chronią odświeżenia. Dla pozostawionej listy timer powiadamia wiązania o wygaśnięciu danych. Stare ulubione i cache pozostają czytelne: nowe pola są opcjonalne, identyfikatory i kolejność nie zmieniają się. Dane metadanych nie dotyczą PDF/numeru jako całości, tematów ani odnośników.

## Panel kontaktowy

### Endpointy na zywo
- `GET json.php?ac=current`
- `GET json.php?ac=schedule`

### Formularz kontaktowy
- `POST json.php?ac=add`
- body:
  - `author`
  - `comment`

### Głosowki
- `POST json.php?ac=addvoice`
- `multipart/form-data`
- pola:
  - `author`
  - `duration_ms`
  - `audio`
- klient Windows najpierw próbuje nagrywać audio lokalnie w trybie `RAW`, a następnie wysyła plik `.m4a`
- jeśli urządzenie nie wspiera `RAW`, aplikacja przechodzi na najlepszy dostępny tryb wejścia bez dodatkowego komunikatu dla użytkownika

## Wymagania i zachowania transportowe
- standardowy timeout requestu: wzorowany na iOS
- retry tylko dla bledow przejsciowych
- omijanie cache lokalnego dla endpointow live
- lokalny cache tresci `WordPress` dla endpointow odczytowych:
  - warstwa pamieci w procesie
  - warstwa dyskowa w `LocalApplicationData/TyfloCentrum.Windows/http-cache`
  - krotkie `TTL` zalezne od typu danych:
    - wyszukiwanie: ~2 minuty
    - feedy, listy wpisow i komentarze: ~5 minut
    - szczegoly wpisu i numeru `TyfloSwiata`: ~10 minut
    - kategorie i lista numerow `TyfloSwiata`: ~30 minut
- cache nie obejmuje endpointow live ani formularzy kontaktowych
- logowanie endpointow bez danych wrazliwych
- klient zgłoszeń zawsze wysyła jawny `User-Agent`, bo ruch bez tego nagłówka może zostać odrzucony przez warstwę `Cloudflare`

## Modele domenowe do odwzorowania
- `Podcast`
- `WPPostSummary`
- `Category`
- `Comment`
- `Schedule`
- `ContactResponse`
- `Availability`

## Wnioski implementacyjne
- warto rozdzielic klientow na:
  - `PodcastApiClient`
  - `TyfloSwiatApiClient`
  - `ContactPanelClient`
  - `PushServiceClient`
- kontrakty odpowiedzi powinny byc testowane fixture'ami
- klient HTTP powinien byc wspolna warstwa z:
  - retry
  - timeout
  - cache policy
  - error mapping

## Powiazane zrodla w bazowej aplikacji
- `/mnt/d/projekty/tyflocentrum/Tyflocentrum/TyfloAPI.swift`
- `/mnt/d/projekty/tyflocentrum/docs/voice-messages.md`
- `/mnt/d/projekty/tyflocentrum/docs/push-notifications.md`

## Push service dla Windows

### Rejestracja klienta WNS
- `POST /api/v1/register`
- body:
  - `token`
  - `env`
  - `prefs`
- klient Windows wysyła:
  - `env = windows-wns`
  - `token = channelUri` z WNS
  - `prefs` w tym samym kształcie co iOS:
    - `podcast`
    - `article`
    - `live`
    - `schedule`

### Wyrejestrowanie klienta WNS
- `POST /api/v1/unregister`
- body:
  - `token`

### Uwagi implementacyjne
- aplikacja Windows ma już klienta synchronizacji do `push-service`, ale pełne E2E wymaga jeszcze backendu wysyłającego do WNS
- repo Windows ma juz osobny backend `TyfloCentrum.PushService`, ktory implementuje:
  - polling `WordPress`
  - `register/update/unregister`
  - webhooki live/schedule
  - wysylke do `WNS`
- żeby kanał WNS działał przy zamkniętej aplikacji, potrzebne są:
  - `Azure App ID`
  - `Azure Object ID`
  - mapowanie `PFN -> Azure App ID` po stronie Microsoft
  - wdrożony serwer z poprawnymi sekretami Azure

## Sygnalista - zgłoszenia błędów i sugestii

### Endpoint
- `POST /v1/report`
- `Content-Type: application/json`
- opcjonalny nagłówek:
  - `x-sygnalista-app-token`

### Publiczne i prywatne dane
- tytuł i opis zgłoszenia trafiają do publicznego issue `GitHub`
- opcjonalny adres e-mail do kontaktu trafia tylko do prywatnego repo intake
- bezpieczna diagnostyka jawna może być pokazana w publicznym issue
- opcjonalny log techniczny jest wysyłany tylko do prywatnego repo intake

### Wymagania prywatnosci i Store
- wysyłka zgłoszenia jest akcją jawną i dobrowolną
- UI musi ostrzegać, że tytuł i opis są publiczne
- UI musi wymagać wyraźnej zgody przed wysłaniem opcjonalnego adresu e-mail do prywatnego repo diagnostycznego
- polityka prywatności musi opisywać:
  - publiczne issue `GitHub`
  - opcjonalny prywatny adres e-mail do kontaktu
  - prywatne repo diagnostyczne dla logów
  - zakres bezpiecznej diagnostyki wysyłanej wraz ze zgłoszeniem
