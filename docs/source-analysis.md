# Analiza aplikacji bazowej iOS

## Kontekst
Projekt Windows ma odtworzyc zachowanie aplikacji iOS z repo:
- `/mnt/d/projekty/tyflocentrum`

Analiza byla wykonana na lokalnym kodzie zrodlowym, bez dodatkowego pobierania z GitHuba.

## Najwazniejsze obserwacje
- Aplikacja bazowa jest natywna, napisana w `SwiftUI`.
- Architektura jest juz rozdzielona na:
  - UI
  - API
  - audio
  - ustawienia
  - ulubione
  - testy
- Dostepnosc nie jest dodatkiem. W kodzie widoczne sa jawne etykiety, identyfikatory, akcje dostepnosci i komunikaty dla `VoiceOver`.
- Znaczna czesc logiki biznesowej opiera sie na prostych kontraktach HTTP do WordPressa i panelu kontaktowego, co ulatwia migracje do .NET.

## Mapa kluczowych plikow

| Obszar | Plik zrodlowy | Rola w aplikacji iOS | Wniosek dla Windows |
|-------|---------------|----------------------|---------------------|
| Start aplikacji | `/mnt/d/projekty/tyflocentrum/Tyflocentrum/TyflocentrumApp.swift` | bootstrap, DI, push, UI testing mode | zachowac podobny bootstrap i podzial zaleznosci |
| Gowna nawigacja | `/mnt/d/projekty/tyflocentrum/Tyflocentrum/Views/ContentView.swift` | zakladki: Nowosci, Podcasty, Artykuly, Szukaj, Tyfloradio | zbudowac odpowiednik shell navigation |
| Warstwa API | `/mnt/d/projekty/tyflocentrum/Tyflocentrum/TyfloAPI.swift` | WordPress API, contact panel, cache, retry | odtworzyc jako zestaw klientow `HttpClient` |
| Odtwarzacz | `/mnt/d/projekty/tyflocentrum/Tyflocentrum/Views/MediaPlayerView.swift` | UI playera, seek, predkosc, znaczniki czasu | to jeden z glownych obszarow parity i a11y |
| Głosowki | `/mnt/d/projekty/tyflocentrum/Tyflocentrum/Views/ContactVoiceMessageView.swift` | nagrywanie, odsluch, wysylka | wymaga spike'u technologicznego na Windows |
| HTML artykulow | `/mnt/d/projekty/tyflocentrum/Tyflocentrum/Views/SafeHTMLView.swift` | sandboxed HTML rendering | do potwierdzenia: `WebView2` vs renderer natywny |
| Ustawienia | `/mnt/d/projekty/tyflocentrum/Tyflocentrum/SettingsStore.swift` | preferencje dostepnosci i playera | bezposrednio przenaszalne jako local settings |
| Ulubione | `/mnt/d/projekty/tyflocentrum/Tyflocentrum/FavoritesStore.swift` | lokalne przechowywanie ulubionych | JSON w local app data wystarczy |
| Smoke testy | `/mnt/d/projekty/tyflocentrum/TyflocentrumUITests/TyflocentrumSmokeTests.swift` | bazowe scenariusze integracyjne | przeniesc jako kontrakty zachowania dla Windows |

## Wnioski architektoniczne
- Nie ma sensu portowac kodu `Swift` 1:1.
- Trzeba zachowac logike domenowa i kontrakty HTTP, ale UI nalezy zaprojektowac natywnie pod Windows.
- Player, HTML i glosowki to trzy obszary najwyzszego ryzyka technicznego.
- Moduly `settings` i `favorites` sa niskiego ryzyka i moga wejsc wczesnie.

## Wnioski dotyczace dostepnosci
- iOS korzysta z wielu recznych komunikatow dla `VoiceOver`.
- Na Windows trzeba odpowiednio zmapowac te zachowania do:
  - `UI Automation`
  - `AutomationProperties`
  - ogloszen statusowych
  - focus management
- Nie nalezy zakladac, ze natywna kontrolka sama rozwiaze cala dostepnosc. Konieczne beda testy `Narrator` i `NVDA`.

## Obszary do bezposredniego odwzorowania
- struktura modulow logicznych
- retry i timeouts w warstwie API
- cache dla odpowiedzi `no-store`
- lista glownej nawigacji
- model ulubionych
- model ustawien
- lista scenariuszy smoke testow

## Obszary wymagajace adaptacji zamiast kopiowania
- `Magic Tap`
- tryb ucha w nagrywaniu
- integracja z systemowym routowaniem audio
- interakcje typowo mobilne

