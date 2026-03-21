# Macierz parytetu funkcji

## Statusy
- `planned` - uzgodnione, bez implementacji
- `in_progress` - pierwsza implementacja jest gotowa, ale obszar nie ma jeszcze pełnego parity
- `spike` - wymaga decyzji technologicznej
- `deferred` - poza `1.0`

| Obszar | Funkcja w iOS | Odpowiednik w Windows | Priorytet | Status | Uwagi |
|-------|----------------|-----------------------|-----------|--------|-------|
| Shell | Zakladki glowne | shell nawigacyjny `WinUI 3` | wysoki | in_progress | działają `Nowości`, `Podcasty`, `Artykuły`, `Szukaj`, `Ulubione`, `Tyfloradio`, `Ustawienia`, skróty `Alt+1` do `Alt+7` i stały, zawsze rozwinięty panel nawigacji |
| Nowosci | wspolny feed | feed mieszany | wysoki | in_progress | lista działa już z domyślną akcją na `Enter`, menu kontekstowym i automatycznym dociąganiem starszych treści przy przewijaniu |
| Podcasty | kategorie i listy | kategorie i listy | wysoki | in_progress | kategorie, lista, domyślne odtwarzanie na `Enter`, menu kontekstowe i automatyczne dociąganie starszych treści są gotowe |
| Artykuly | kategorie i listy | kategorie i listy | wysoki | in_progress | kategorie, lista, otwieranie artykułu w aplikacji na `Enter`, widok czytania, menu kontekstowe i automatyczne dociąganie starszych treści są gotowe |
| TyfloSwiat | strony i numery | strony i numery | wysoki | in_progress | działa pierwszy wariant: roczniki, numery, PDF, spis treści, szczegóły stron TyfloŚwiata, szybkie dodawanie stron do ulubionych i udostępnianie linków |
| Wyszukiwarka | zakres i wyniki | zakres i wyniki | wysoki | in_progress | zakres, wyniki, domyślne akcje na `Enter` i menu kontekstowe są gotowe |
| Szczegoly | podcast detail | podcast detail | wysoki | in_progress | pierwsza wersja szczegółów z treścią tekstową, udostępnianiem, ulubionymi i otwarciem w przeglądarce jest gotowa |
| Szczegoly | article detail | article detail | wysoki | in_progress | pierwsza wersja szczegółów z treścią tekstową, udostępnianiem, ulubionymi i otwarciem artykułu w aplikacji jest gotowa |
| HTML | bezpieczne renderowanie | `WebView2` lub renderer natywny | wysoki | in_progress | działa pierwszy wariant `WebView2` jako widok czytania oparty o treść z API; pozostaje dalsze strojenie UX i testy screen readerów |
| Player | podcast playback | podcast playback | wysoki | in_progress | pierwsza wersja wbudowanego odtwarzacza jest gotowa: play/pause, seek, skoki 30 s, prędkość, wznawianie pozycji, skróty klawiaturowe oraz dodatki z komentarzy |
| Player | live radio | live radio | wysoki | in_progress | sekcja `Tyfloradio`, status, ramówka i wbudowany odtwarzacz strumienia są gotowe |
| Player | znaczniki czasu | chapter markers | wysoki | in_progress | działa pierwszy wariant: parser z komentarzy, lista znaczników i przejście do wybranego czasu |
| Player | linki powiazane | related links | sredni | in_progress | działa pierwszy wariant: parser z komentarzy, otwieranie odnośników w systemowej przeglądarce oraz kopiowanie i udostępnianie linków |
| Ulubione | podcasty, artykuly, tematy, linki | to samo | wysoki | in_progress | działa rozszerzona wersja: podcasty, artykuły, strony TyfloŚwiata, tematy z timestampów i linki powiązane; działa też kopiowanie i udostępnianie linków, pozostaje dalsze dopracowanie UX |
| Ustawienia | typ tresci, predkosc, push | to samo | wysoki | in_progress | działa wybór urządzenia wejściowego i wyjściowego, domyślna prędkość odtwarzania i zapamiętywanie ostatniej prędkości; push dalej zależy od decyzji |
| Komentarze | lista i szczegoly | lista i szczegoly | sredni | in_progress | działa lista komentarzy podcastu, licznik komentarzy i szczegóły pojedynczego komentarza |
| Kontakt | formularz tekstowy | formularz tekstowy | wysoki | in_progress | formularz tekstowy, walidacja, wysyłka i szkic lokalny są gotowe |
| Kontakt | glosowka | glosowka | wysoki | in_progress | pierwsza wersja działa: preferowany `RAW` capture z cichym fallbackiem, append, `przytrzymaj i mów`, komunikaty przerwań, odsłuch, usunięcie i upload; do domknięcia pozostaje tryb ucha |
| Dostepnosc | komunikaty VoiceOver | UIA + status announcements | wysoki | planned | kluczowe dla `1.0` |
| Push | iOS push | Windows push | sredni | spike | decyzja `1.0` vs `1.1` |
| AirPlay | wybór urzadzenia | systemowy routing audio | niski | planned | adaptacja, nie kopia 1:1 |
| Magic Tap | globalna akcja VO | skroty klawiaturowe | sredni | in_progress | działają skróty sekcji `Alt+1` do `Alt+7` i skróty odtwarzacza `Ctrl+spacja`, `Ctrl+strzałki` |
| Tryb ucha | proximity recording | brak odpowiednika lub uproszczenie | niski | deferred | tylko jesli niski koszt |
