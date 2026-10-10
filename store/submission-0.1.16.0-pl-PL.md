# TyfloCentrum 0.1.16.0, materiały wydania Microsoft Store

## Wersja pakietu
* `0.1.16.0`
* architektura: `x64`
* czwarty segment wersji zawsze `0`

## Zawartość wydania
Scalony PR 28, HEAD `371d4e29e2743c32c00db0f304abf1e82058f305`, commit scalenia `148398727be153674ae8c75ff7261e27c632bc54`. Drzewo gałęzi `main` po scaleniu jest identyczne z drzewem odebranego commita: `b1eec8cf543e3bedb32250160fc5f8dc26ca5f70`.

## Changelog wobec 0.1.15.0
* czasy publikacji i czasy trwania treści aktualizują się w istniejących wierszach, bez rekreacji elementów listy i bez restartu aplikacji
* po F5 fokus pozostaje na tym samym elemencie, a pozycja przewinięcia listy nie jest resetowana
* odtwarzanie podcastu nie jest przerywane w trakcie odświeżania czasów
* do kolejki czytnika ekranu trafia jedna aktualizacja czasu dla zmienionej wartości; brak zmiany nie generuje ogłoszenia
* awaria batcha metadanych nie nadpisuje świeższych danych już obecnych w modelu widoku
* trzy pionowe kontenery StackPanel zastąpione Grid z wierszem gwiazdkowym w wyszukiwaniu, ulubionych i hoście czasopisma, co przywraca użyteczny obszar przewijania długich list
* sonda UIA do pomiarów kompiluje się wyłącznie przy jawnym `ContentTimeUiProbe=true`, więc nie wchodzi do pakietu sklepowego

## Podstawa wydania
Odbiór rodzica na maszynie GARFIELD: 343 testy Passed, 1 NotExecuted (istniejący scaffold, nieliczony jako zaliczenie), w tym 143 testy ContentTime Passed. Zmierzono 8 ścieżek F5 wraz z powrotem z tła, 24 okna odtwarzania oraz kolejkę mowy NVDA.

## Granice pomiaru
Kolejka mowy mierzona była na torze `pre_speechQueued` przy syntezatorze `silence`. Nie wykonano pomiaru akustycznego z głośnika ani badania Narratora. Pomiary HTTP i MVVM nie zastępują fizycznego przejścia pełnej populacji wierszy.

## Description
Pierwsza linia opisu pozostaje bez zmian, inaczej wraca ostrzeżenie certyfikacji o zależności od runtime:

```text
TyfloCentrum wymaga do działania Microsoft .NET Desktop Runtime.
```

Źródło prawdy dla automatu: `store/listing/pl-PL.description.txt`.

## What's new in this version
Źródło prawdy: `store/listing/pl-PL.whatsnew.txt`. Treść wysyłana w tym wydaniu:

```text
Czasy publikacji i czasy trwania treści odświeżają się teraz w już wyświetlonych wierszach, bez ponownego uruchamiania aplikacji. Po naciśnięciu F5 fokus pozostaje na tym samym elemencie, a lista nie przeskakuje na początek, więc można wrócić dokładnie tam, gdzie się było. Odtwarzanie podcastu trwa dalej podczas odświeżania. Czytnik ekranu dostaje jedną aktualizację czasu zamiast serii powtórzeń, a gdy czas się nie zmienił, nie dostaje ogłoszenia. Poprawiono też obszar przewijania długich list w wyszukiwaniu, ulubionych i TyfloŚwiecie.
```

## Publikacja
Workflow `store-release.yml`, wejścia `git_ref=main`, `platform=x64`, `rollout_percentage` puste (pełne wydanie). Workflow buduje MSIX, pakuje `.msixupload` i składa submisję przez Store REST API skryptem `scripts/store/store_publish.py --commit`. Ręczne wklejanie tekstów w Partner Center nie jest potrzebne.

## Weryfikacja po wysłaniu
Przed tym wydaniem ostatnia opublikowana submisja zawierała pakiet `0.1.15.0 x64` ze statusem `Published`, bez submisji oczekującej. Po commicie należy odczytać przez API wersję pakietu w submisji oraz jej status. Status typu oczekiwanie na certyfikację oznacza wysłane do sklepu, nie publicznie dostępne.
