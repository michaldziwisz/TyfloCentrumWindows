# Standardy inzynierskie

## Zasady ogolne
- projekt pozostaje natywny dla Windows
- preferowany styl architektoniczny to `MVVM`
- logika biznesowa nie trafia do code-behind widokow
- zaleznosci zewnetrzne dobierac konserwatywnie
- przy zmianach architektury aktualizowac dokumentacje i ADR

## Konwencje warstw
- `UI`:
  - widoki
  - kontrolki
  - style
  - nawigacja
- `Domain`:
  - modele
  - use case'y
  - interfejsy serwisow
- `Infrastructure`:
  - HTTP
  - storage
  - audio
  - notyfikacje

## Konwencje UI
- preferowac natywne kontrolki XAML
- unikac custom controls, jesli mozna osiagnac ten sam efekt w prostszy sposob
- przy custom control wymagane jest:
  - zachowanie klawiatury
  - nazwa dostepna
  - poprawny stan
  - plan testu `Narrator`

## Konwencje dostepnosci
- kazdy element interaktywny ma jawne `AutomationProperties`
- focus nie moze byc skutkiem ubocznym, tylko zaprojektowanym zachowaniem
- dynamiczne komunikaty maja byc odczytywalne
- nie ukrywac bledow w logach. Uzytkownik musi dostac czytelny komunikat

## Konwencje dla HTTP i danych
- wszystkie klienty HTTP przechodza przez wspolna warstwe obslugujaca:
  - timeout
  - retry
  - logging
  - cache policy
- kontrakty API testowac fixture'ami
- nie mieszac modeli transportowych z modelami widokow bez uzasadnienia

## Konwencje testowe
- kazda nowa polityka, parser lub adapter ma test jednostkowy
- kazdy krytyczny ekran ma co najmniej smoke scenariusz UI
- zmiany w dostepnosci wymagaja aktualizacji checklist manualnych

## Konwencje dokumentacyjne
- `README.md` i `docs/index.md` sa punktami startowymi
- duze decyzje trafiaja do `docs/adr/`
- backlog i roadmapa maja pozostac spojne z architektura

## Antywzorce
- logika biznesowa w code-behind
- ad hoc wstrzykiwanie zaleznosci bez wspolnej kompozycji
- zmiany stacku bez dokumentacji
- traktowanie testow dostepnosci jako opcjonalnych

