# Wymagania dostepnosci

## Cel
Wersja Windows ma byc projektowana jako aplikacja dostepna od pierwszych iteracji. Dostepnosc nie jest etapem koncowym ani warstwa poprawek po wdrozeniu.

## Docelowe technologie wspomagajace
- `Narrator` - wymagany
- `NVDA` - wymagany
- `JAWS` - test rozszerzony, jesli bedzie dostepny

## Wymagania ogolne
- pelna obsluga klawiatura bez potrzeby myszy
- zgodnosc z `WCAG 2.2 AA` tam, gdzie ma to zastosowanie do aplikacji desktopowej
- wykorzystanie natywnego `UI Automation`
- przewidywalny focus order
- widoczne focus indicators
- brak niestandardowych kontrolek bez jawnego modelu automatyzacji

## Zasady projektowe
- preferowac natywne kontrolki XAML
- kazdy element interaktywny ma:
  - dostepna nazwe
  - poprawny stan
  - czytelny opis celu
- komunikaty sukcesu, bledu i ladowania maja byc odczytywalne bez recznego szukania ich w drzewie UI

## Wymagania dla glownego shellu
- focus po starcie trafia na logiczny element pierwszego ekranu
- zmiana sekcji nie gubi fokusu
- aktywna sekcja jest czytelnie komunikowana
- menu aplikacji jest w pelni obslugiwalne klawiatura
- skroty sekcji musza byc jawnie ujawnione w UI lub `HelpText`

## Wymagania dla list i szczegolow
- pozycje listy maja czytelna nazwe i wartosc
- lista nie moze gubic fokusu po asynchronicznym doladowaniu
- powrot ze szczegolow przywraca logiczne miejsce pracy uzytkownika
- akcje dodatkowe sa dostepne bez myszy

## Wymagania dla wyszukiwarki
- uzytkownik moze wpisac fraze, uruchomic wyszukiwanie klawiszem Enter i od razu otrzymac komunikat o wyniku
- liczba wynikow, brak wynikow i blad sa oglaszane czytnikowi

## Wymagania dla formularzy
- kazde pole ma etykiete
- bledy walidacyjne sa jawne i czytelne
- po nieudanym wyslaniu focus wraca do pierwszego pola z bledem lub do podsumowania bledow
- pola wymagane sa jednoznacznie oznaczone

## Wymagania dla playera
- kontrolki playera maja poprawne nazwy i stany
- odczytywane sa:
  - play/pause
  - pozycja
  - czas trwania
  - predkosc
- seek `+-30s` ma byc dostepny z klawiatury
- play/pause ma miec przewidywalny skrot klawiaturowy
- skroty playera musza byc opisane w samym UI
- zmiana predkosci i pozycji daje komunikat statusowy
- live stream ma byc odroznialny od zwyklego odcinka

## Wymagania dla znacznikow czasu i linkow
- lista znacznikow czasu jest odczytywalna liniowo
- kazdy znacznik ma:
  - tytul
  - czas
  - akcje dodatkowe
- linki powiazane nie moga byc anonimowe

## Wymagania dla tresci HTML
- sposob czytania musi byc zweryfikowany w `Narrator` i `NVDA`
- osadzona tresc nie moze tworzyc pulapek focusu
- linki otwieraja sie bezpiecznie i przewidywalnie
- dlugie tresci pozostaja czytelne przy zwiekszonym tekscie

## Wymagania dla glosowek
- nagrywanie musi byc w pelni obslugiwalne z klawiatury
- stan nagrywania jest jednoznacznie sygnalizowany
- pierwszy prompt zgody na mikrofon ma byc wywolywany przez system z poziomu scenariusza nagrywania, a nie odsylac od razu do ustawien
- blad mikrofonu lub brak uprawnien jest komunikowany wprost
- przerwanie nagrywania przez system albo utrata mikrofonu jest oglaszana czytnikowi ekranu bez dodatkowej akcji uzytkownika
- odsluch i wysylka nie moga wymagac interakcji wskaznikowej
- brak wsparcia `RAW` dla mikrofonu nie moze blokowac nagrania, jesli Windows potrafi uruchomic inne dostepne przetwarzanie wejscia

## Wymagania dla stanu aplikacji
- `loading`
- `empty`
- `error`
- `success`

Dla kazdego z tych stanow trzeba zdefiniowac:
- co jest widoczne
- co jest fokusowalne
- co jest oglaszane

## Minimalna checklista wydaniowa
- test klawiatura-only
- test `Narrator`
- test `NVDA`
- test wysokiego kontrastu
- test zwiekszonego tekstu
- test najwazniejszych sciezek:
  - otwarcie aplikacji
  - nawigacja po sekcjach
  - wyszukanie tresci
  - odtworzenie podcastu
  - odczyt artykulu
  - wyslanie formularza kontaktowego

## Definition of Done dla zmian UI
- kazdy nowy element interaktywny ma nazwe dostepna przez `UI Automation`
- klawiatura przechodzi przez ekran w logicznej kolejnosci
- scenariusz zostal sprawdzony manualnie przynajmniej w `Narrator`
- jesli ekran jest krytyczny, zostal sprawdzony rowniez w `NVDA`
- testy i dokumentacja zostaly zaktualizowane
