# ADR 0002 - Warstwowy uklad solution

## Status
Accepted

## Data
2026-03-19

## Kontekst
Projekt ma byc rozwijany iteracyjnie przez agenta i czlowieka, z duzym naciskiem na:
- czytelna separacje odpowiedzialnosci
- testowalnosc
- kontrolowana integracje z Windows API
- ograniczenie ryzyka wrzucania logiki biznesowej do XAML code-behind

## Decyzja
Solution dzielimy na cztery glowne projekty:
- `Tyflocentrum.Windows.App`
- `Tyflocentrum.Windows.UI`
- `Tyflocentrum.Windows.Domain`
- `Tyflocentrum.Windows.Infrastructure`

Osobno utrzymujemy projekty testowe:
- `Tyflocentrum.Windows.Tests`
- `Tyflocentrum.Windows.UITests`

## Uzasadnienie
- `App` powinno zostac cienka warstwa bootstrap i hostingu.
- `UI` powinno byc miejscem dla ViewModeli i wspolnych elementow prezentacji.
- `Domain` powinno byc najstabilniejsza warstwa, niezalezna od Windows-specific API.
- `Infrastructure` powinno izolowac HTTP, storage, audio i przyszle integracje systemowe.

## Konsekwencje

### Pozytywne
- latwiejsze testowanie logiki
- mniejsza szansa na chaos w warstwie UI
- prostsze utrzymanie i refaktoryzacja

### Negatywne
- wiecej projektow do utrzymania
- wieksza liczba decyzji na starcie przy dodawaniu nowego kodu

## Zasady wynikajace z decyzji
- `Domain` nie zalezy od `UI` ani `Infrastructure`
- `Infrastructure` moze zalezec od `Domain`
- `UI` moze zalezec od `Domain`
- `App` moze zalezec od wszystkich pozostalych warstw

