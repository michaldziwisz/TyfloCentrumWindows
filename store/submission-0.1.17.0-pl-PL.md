# TyfloCentrum 0.1.17.0, materiały wydania Microsoft Store

## Wersja pakietu
* `0.1.17.0`
* architektura: `x64`
* czwarty segment wersji zawsze `0`

## Zawartość wydania
Poprawka zgłoszenia [#29](https://github.com/michaldziwisz/TyfloCentrumWindows/issues/29): w formularzu komentarza w odtwarzaczu podcastu pole edycji traciło fokus po każdym wpisanym znaku.

## Changelog wobec 0.1.16.0
* przyczyna: każda zmiana pola formularza (imię, e-mail, treść) wywoływała PropertyChanged modelu widoku, a jego obsługa w odtwarzaczu przebudowywała cały panel dodatków odcinka; pole z fokusem było usuwane z drzewa i fokus przechodził na przycisk Zamknij
* formularz jest teraz aktualizowany w miejscu: nagłówek, informacja o odpowiedzi, przycisk Anuluj odpowiedź, stan przycisku wysyłania i wartości pól; tak samo działał już formularz w dialogu komentarzy
* panel jest przebudowywany tylko przy jawnych akcjach użytkownika (pokazanie formularza, anulowanie odpowiedzi, wysłanie)

## Podstawa wydania
* CI: pełne solution, 339 testów Windows.Tests i 4 testy PushService.Tests Passed
* pomiar UIA na zainstalowanym buildzie Release, maszyna GARFIELD, droga użytkownika: Podcasty, Enter na odcinku, Dodaj komentarz, znaki SendInput
* kontrola bez poprawki (0.1.16, `b1dadf8`): po pierwszym znaku fokus na przycisku Zamknij, 0 z 2 znaków z zachowanym fokusem, w obu polach
* z poprawką: 8 z 8 znaków z zachowanym fokusem, ten sam element (RuntimeId) przez cały czas, wartości pól `Sonda` i `abc`

## Granice pomiaru
Nie wykonano odsłuchu NVDA ani Narratora; pomiar dotyczy fokusu UIA i wartości pól. Nie wysyłano komentarza na serwer.

## Description
Pierwsza linia opisu pozostaje bez zmian, inaczej wraca ostrzeżenie certyfikacji o zależności od runtime:

```text
TyfloCentrum wymaga do działania Microsoft .NET Desktop Runtime.
```

Źródło prawdy dla automatu: `store/listing/pl-PL.description.txt`.

## What's new in this version
Źródło prawdy: `store/listing/pl-PL.whatsnew.txt`. Treść wysyłana w tym wydaniu:

```text
Naprawiono pisanie komentarzy w odtwarzaczu podcastu. Wcześniej po wpisaniu każdego znaku pole edycji traciło fokus i trzeba było wracać do niego, żeby pisać dalej. Teraz w polach Imię, Adres e-mail i Treść komentarza można pisać bez przerw, a czytnik ekranu zostaje w polu, w którym się pisze.
```

## Publikacja
Workflow `store-release.yml`, wejścia `git_ref=main`, `platform=x64`, `rollout_percentage` puste (pełne wydanie). Submisja przez Store REST API skryptem `scripts/store/store_publish.py --commit`.

## Weryfikacja po wysłaniu
Przed tym wydaniem ostatnia opublikowana submisja zawierała pakiet `0.1.16.0 x64` ze statusem `Published`, bez submisji oczekującej. Po commicie należy odczytać przez API wersję pakietu w submisji oraz jej status.
