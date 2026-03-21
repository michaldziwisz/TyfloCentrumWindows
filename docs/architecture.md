# Architektura docelowa

## Cele architektoniczne
- zachowac natywny charakter aplikacji Windows
- utrzymac silna separacje miedzy UI, domena i infrastruktura
- maksymalnie uproscic testowanie logiki bez uruchamiania UI
- wbudowac wymagania dostepnosci w warstwe komponentow i nawigacji
- przygotowac aplikacje do pakowania jako `MSIX`

## Stos technologiczny
- `WinUI 3`
- `Windows App SDK`
- `.NET 8`
- `CommunityToolkit.Mvvm`
- `HttpClient`
- `System.Text.Json`
- `Microsoft.Extensions.DependencyInjection`
- `Microsoft.Extensions.Logging`

## Planowany uklad solution

### `src/Tyflocentrum.Windows.App`
Odpowiada za:
- punkt startowy aplikacji
- bootstrap DI
- konfiguracje
- shell
- integracje systemowe zwiazane z pakowaniem

### `src/Tyflocentrum.Windows.UI`
Odpowiada za:
- widoki
- ViewModel'e
- kontrolki
- nawigacje
- focus management
- zachowania dostepnosci i automatyzacji

### `src/Tyflocentrum.Windows.Domain`
Odpowiada za:
- modele domenowe
- use case'y
- polityki odtwarzania i wyszukiwania
- kontrakty serwisow

### `src/Tyflocentrum.Windows.Infrastructure`
Odpowiada za:
- klientow HTTP
- storage lokalny
- integracje audio
- integracje powiadomien
- adaptery systemowe

### `tests/*`
Odpowiada za:
- testy jednostkowe
- testy kontraktowe
- smoke testy UI
- automatyzacje kluczowych scenariuszy

## Zasady zaleznosci
- `App` zalezy od `UI`, `Domain`, `Infrastructure`
- `UI` zalezy od `Domain`
- `Infrastructure` zalezy od `Domain`
- `Domain` nie zalezy od `UI` ani `Infrastructure`

## Nawigacja
- shell ma odwzorowywac glowne sekcje produktu:
  - Nowosci
  - Podcasty
  - Artykuly
  - Szukaj
  - Tyfloradio
- dodatkowe obszary:
  - Ulubione
  - Ustawienia
- nawigacja ma byc testowalna i jawnie zarzadzac focusem po zmianie ekranu

## Stan aplikacji
- stan widokow:
  - `loading`
  - `loaded`
  - `empty`
  - `error`
- dane ekranow powinny byc dostarczane przez ViewModel'e, nie bezposrednio z widoku
- ViewModel nie powinien znac konkretnych kontrolek `WinUI`

## Integracje danych
- WordPress:
  - podcasty
  - artykuly
  - kategorie
  - komentarze
  - strony
- panel kontaktowy:
  - `current`
  - `schedule`
  - `add`
  - `addvoice`
- opcjonalny push service:
  - tokeny
  - preferencje
  - deep linki

## Cache i odpornosc
- warstwa HTTP powinna odtwarzac zachowanie obecnej aplikacji iOS:
  - timeouty
  - retry dla bledow przejsciowych
  - cache protokolowy dla odpowiedzi cache-friendly
  - dodatkowy memory cache dla odpowiedzi `no-store`
- endpointy na zywo omijaja cache lokalny

## Odtwarzanie audio
- player oparty o natywne API Windows
- wymagane:
  - play/pause
  - seek
  - predkosc
  - resume
  - integracja z systemowymi klawiszami multimedialnymi
- UI playera ma byc kontrolowane przez wlasny ViewModel, nawet jesli pod spodem sa natywne kontrolki systemowe

## Renderowanie HTML
- decyzja technologiczna wymaga spike'u
- warianty:
  - `WebView2` w trybie ograniczonym
  - parser i renderer do natywnych kontrolek
- kryterium wyboru:
  - jakosc odczytu przez `Narrator` i `NVDA`
  - bezpieczenstwo
  - koszt utrzymania

## Persistencja lokalna
- ustawienia:
  - `ApplicationData.LocalSettings`
- ulubione i dane lokalne:
  - JSON w `ApplicationData.LocalFolder`
- pozycje odtwarzania:
  - osobny magazyn lokalny z prostym modelem i migracja wersji

## Powiadomienia
- architektura ma zostawic miejsce na push, ale ostateczny zakres zalezy od decyzji produktowej
- jesli push wchodzi do `1.0`, trzeba uwzglednic to w modelu pakowania i pipeline release

## Pakowanie i Store
- aplikacja od poczatku pozostaje packaged app
- glowny target dystrybucji to Microsoft Store
- `MSIX` jest artefaktem release, nie dodatkiem na koncu projektu

## Ryzyka architektoniczne
- renderowanie HTML
- nagrywanie i laczenie glosowek
- wsparcie push
- zakres wspieranych wersji Windows

## Dokumenty powiazane
- [Wymagania produktowe](product-requirements.md)
- [Wymagania dostepnosci](accessibility-requirements.md)
- [Integracje API](api-integrations.md)
- [Release MSIX i Store](release-msix-store.md)
- [ADR 0001](adr/0001-winui3-windowsappsdk-msix.md)

