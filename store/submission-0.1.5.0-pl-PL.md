# TyfloCentrum 0.1.5.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.5.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.5.0_x64.msixupload`

## Description
Początek opisu powinien pozostać bez zmian, żeby nie wróciło ostrzeżenie o zależności od runtime:

```text
TyfloCentrum wymaga do działania Microsoft .NET Desktop Runtime.
TyfloCentrum to dostępna aplikacja dla systemu Windows, która zbiera w jednym miejscu treści z ekosystemu TyfloPodcast. Pozwala wygodnie przeglądać aktualności, artykuły i podcasty, słuchać TyfloRadia, korzystać z ulubionych, pobierać materiały do czytania i odsłuchu offline oraz wysyłać wiadomości tekstowe i głosowe do redakcji.
```

Resztę opisu skopiuj z:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## What's new in this version
```text
W tej wersji poprawiono odświeżanie treści przy dłużej otwartej aplikacji. Sekcje Nowości, Podcasty i Artykuły automatycznie pobierają nowe wpisy po kilku minutach, dzięki czemu nowo opublikowane audycje i artykuły pojawiają się bez restartu programu. Jednocześnie ograniczono ryzyko duplikatów przy dalszym doładowywaniu starszych treści.

Uproszczono też nawigację w głównym shellu aplikacji. Usunięto zbędny komunikat o wersji testowej, a skróty sekcji są teraz widoczne bezpośrednio przy nazwach pozycji na liście, na przykład Nowości (Alt+1) i Podcasty (Alt+2). Poprawiono również zachowanie dostępności, tak aby czytniki ekranu nie zatrzymywały się już na osobnych, technicznych popupach od skrótów klawiaturowych.
```

## Changelog vs poprzednia opublikowana wersja
- sekcje `Nowości`, `Podcasty` i `Artykuły` automatycznie odświeżają nowe treści podczas dłuższego działania aplikacji
- auto-odświeżanie dokłada nowe wpisy na początek listy bez pełnego resetu widoku
- doładowywanie starszych treści pomija duplikaty po wcześniejszym odświeżeniu
- usunięty został komunikat `Wersja testowa Windows`
- skróty sekcji są widoczne bezpośrednio w nazwach pozycji listy sekcji
- ukryto popupy techniczne od `KeyboardAccelerators`, które były widoczne dla czytników ekranu jako zbędne elementy

## Product features
Na ten release nie trzeba ich zmieniać. Aktualna wersja jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.5.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
