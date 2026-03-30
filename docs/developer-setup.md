# Setup deweloperski

## Cel
Opisac minimalne srodowisko potrzebne do pracy nad aplikacja Windows oraz ograniczenia pracy z poziomu WSL.

## Wymagane srodowisko do realnej implementacji i uruchamiania
- Windows 11 lub Windows 10 z aktualnym SDK zgodnym z celem projektu
- Visual Studio 2022
- workload do:
  - `.NET desktop development`
  - `Windows application development`
- Windows SDK zgodny z targetem projektu
- .NET SDK 8

## Narzedzia opcjonalne
- Git
- PowerShell 7
- `winget`
- `dotnet` CLI

## Ograniczenia WSL
- WSL nie jest samodzielnym srodowiskiem Windows, ale moze sterowac narzedziami Windows z linii polecen:
  - `dotnet.exe`
  - `powershell.exe`
  - inne narzedzia zainstalowane po stronie Windows
- To wystarcza do:
  - restore
  - build
  - uruchamiania testow jednostkowych
  - podstawowej walidacji artefaktow wyjsciowych
- To nadal nie wystarcza do wiarygodnej walidacji:
  - packaged app uruchamianej interaktywnie
  - testow `MSIX`
  - testow `Narrator`
  - testow `NVDA`
  - testow UI automation wymagajacych aktywnego desktopu Windows

## Co mozna robic z WSL
- prace na dokumentacji
- edycje kodu i struktury solution
- przygotowanie plikow projektu
- przeglady i refaktoryzacje tekstowych plikow
- wywolywanie Windows `dotnet.exe` do:
  - restore
  - build
  - test
- wywolywanie `powershell.exe` do zadan pomocniczych

## Co wymaga natywnego Windows
- uruchomienie aplikacji
- testy `Narrator`
- testy `NVDA`
- pakowanie i walidacja `MSIX`
- debugowanie zachowan okien i kontrolek `WinUI`
- publikacja i finalna walidacja Microsoft Store

## Minimalna procedura startu na Windows
1. Otworzyc `TyfloCentrum.Windows.sln` w Visual Studio 2022.
2. Pozwolic Visual Studio przywrocic pakiety NuGet.
3. Zweryfikowac, czy workloadi i SDK sa kompletne.
4. Wybrac `x64` jako pierwsza platforme uruchomieniowa.
5. Uruchomic scaffold aplikacji.

## Zweryfikowane komendy z WSL
- build:
  ```bash
  WIN_SOLUTION=$(wslpath -w /mnt/d/projekty/tyflocentrum_pc/TyfloCentrum.Windows.sln)
  '/mnt/c/Program Files/dotnet/dotnet.exe' build "$WIN_SOLUTION" -c Debug -p:Platform=x64
  ```
- test:
  ```bash
  WIN_SOLUTION=$(wslpath -w /mnt/d/projekty/tyflocentrum_pc/TyfloCentrum.Windows.sln)
  '/mnt/c/Program Files/dotnet/dotnet.exe' test "$WIN_SOLUTION" -c Debug -p:Platform=x64
  ```

## Pierwsze rzeczy do zweryfikowania po otwarciu solution na Windows
- restore NuGet
- build solution
- start packaged app
- poprawne wczytanie assetow z manifestu
- uruchomienie shellu z sekcjami:
  - Nowosci
  - Podcasty
  - Artykuly
  - Szukaj
  - Tyfloradio

## Jak uruchamiac artefakt testowy
- nie uruchamiaj bezposrednio:
  - `src/TyfloCentrum.Windows.App/bin/x64/Debug/net8.0-windows10.0.19041.0/TyfloCentrum.Windows.App.exe`
- ten plik nie jest poprawnym artefaktem testowym dla packaged app i konczy sie bledem startu Windows App SDK bez tozsamosci pakietu
- poprawny test lokalny powinien isc przez katalog `AppPackages`, na przyklad:
  - `artifacts/SignedAppPackagesV3/TyfloCentrum.Windows.App_0.1.0.0_x64_Debug_Test/Install.ps1`
- `Install.ps1` nalezy uruchomic w Windows PowerShell
- jesli skrypt poprosi o podniesione uprawnienia do instalacji certyfikatu, uruchom go w podniesionym PowerShell albo przez `Run with PowerShell`
- repo ma tez skrypty pomocnicze:
  - `scripts/windows/Build-DevMsix.ps1`
  - `scripts/windows/Install-DevMsix.ps1`
- rekomendowany workflow testowy:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\windows\Build-DevMsix.ps1 -Install`
- ten workflow:
  - buduje nowy pakiet
  - generuje paczke aplikacji `.msix` i paczke symboli `.appxsym`
  - usuwa zainstalowany pakiet testowy `TyfloCentrum.Windows` oraz pozostalosci po poprzednich wariantach, jesli sa obecne
  - instaluje nowy build

## Jak zbudowac wersje direct MSI poza Store
- repo ma tez osobny skrypt:
  - `scripts/windows/Build-DirectMsi.ps1`
- rekomendowane uzycie:
  - `powershell -ExecutionPolicy Bypass -File .\scripts\windows\Build-DirectMsi.ps1`
- ten workflow:
  - publikuje aplikacje jako `unpackaged`
  - buduje standardowy instalator `MSI` przez `WiX`
  - zapisuje wynik w `artifacts/DirectMsi/...`
  - nie polega na pakiecie `MSIX` ani na skryptach `Install.ps1`

## Powiazane dokumenty
- [README repo](../README.md)
- [Architektura](architecture.md)
- [Pakowanie MSIX i Microsoft Store](release-msix-store.md)
- [Dystrybucja poza Store jako direct MSI](release-direct-msi.md)
