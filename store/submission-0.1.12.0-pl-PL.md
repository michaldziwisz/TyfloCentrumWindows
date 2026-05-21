# TyfloCentrum 0.1.12.0 — materialy do recznej aktualizacji Microsoft Store

## Wersja pakietu
- `0.1.12.0`

## Packages
Po zbudowaniu release wrzuc do `Packages` plik:

- `TyfloCentrum.Windows.App_0.1.12.0_x64.msixupload`

Jesli budujesz go lokalnie, przygotuj go tak:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\New-StoreMsixUpload.ps1 -PackageDirectory .\artifacts\SignedAppPackages\TyfloCentrum.Windows.App_0.1.12.0_x64_Test
```

Do `msixupload` trafia tylko `.msix` oraz opcjonalnie `.appxsym`. Plik `.cer` musi zostac poza archiwum.

## Description
Poczatek opisu powinien pozostac bez zmian, zeby nie wrocilo ostrzezenie o zaleznosci od runtime:

```text
TyfloCentrum wymaga do działania Microsoft .NET Desktop Runtime.
TyfloCentrum to dostępna aplikacja dla systemu Windows, która zbiera w jednym miejscu treści z ekosystemu TyfloPodcast. Pozwala wygodnie przeglądać aktualności, artykuły i podcasty, słuchać TyfloRadia, korzystać z ulubionych, pobierać materiały do czytania i odsłuchu offline oraz wysyłać wiadomości tekstowe i głosowe do redakcji.
```

Reszte opisu skopiuj z:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## What's new in this version
```text
Poprawiono ramówkę Tyfloradia: linki w ramówce są interaktywne, klawisz Escape zamyka ramówkę, a powrót z osobnej sekcji ramówki prowadzi z powrotem do listy kategorii. Usprawniono wysyłanie zgłoszeń błędów i sugestii: aplikacja nie pokazuje już fałszywego błędu po poprawnym utworzeniu zgłoszenia i wyświetla jednoznaczny dialog z wynikiem wysyłki. W odtwarzaczu podcastów dodano obsługę skrótów Ctrl+K dla komentarzy i Ctrl+T dla znaczników czasu.
```

## Changelog vs poprzednia opublikowana wersja
- ramówka Tyfloradia obsługuje interaktywne linki i zamykanie klawiszem `Escape`
- osobna sekcja `Ramówka Tyfloradia` nie pokazuje komunikatu o braku audycji interaktywnej i po zamknięciu wraca do listy kategorii
- zgłoszenia błędów i sugestii poprawnie rozpoznają udane utworzenie publicznego issue także przy nietypowej odpowiedzi serwera
- po wysłaniu zgłoszenia pojawia się dialog z jasnym komunikatem i opcją otwarcia publicznego zgłoszenia
- pole opisu zgłoszenia nie blokuje już wyświetlenia komunikatu po wysłaniu formularza
- klient zgłoszeń ma dłuższy timeout dla operacji z logami i GitHubem
- w odtwarzaczu podcastów działają skróty `Ctrl+K` do komentarzy i `Ctrl+T` do znaczników czasu
- w pomocy skrótów odtwarzacza dopisano informacje o `Ctrl+K` i `Ctrl+T`

## Product features
Na ten release nie trzeba ich zmieniać. Aktualna wersja jest w:

- [listing-pl-PL.md](/mnt/d/projekty/tyflocentrum_pc/store/listing-pl-PL.md)

## Checklist submission
- `Packages`: podmień pakiet na `0.1.12.0`
- `Store listings`: wklej nowe `What's new in this version`
- `Description`: zostaw linię o `.NET Desktop Runtime` na początku
- pozostałe sekcje bez zmian, jeśli Partner Center nie oznaczy ich jako incomplete
