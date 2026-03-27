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
- `GET /wp/v2/categories`
- `GET /wp/v2/comments`

### Zachowania do odtworzenia
- paginacja przez `page` i `per_page`
- odczyt naglowkow:
  - `X-WP-Total`
  - `X-WP-TotalPages`
- filtrowanie po kategorii
- wyszukiwanie po parametrze `search`
- ograniczanie zwracanych pol przez `_fields`

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
