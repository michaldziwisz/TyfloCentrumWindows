# Dystrybucja poza Store jako standardowy instalator

## Cel
Utrzymac niezalezny od `MSIX` kanal instalacji dla uzytkownikow, ktorzy nie chca korzystac ze Store i oczekuja klasycznego instalatora Windows z wyborem katalogu docelowego.

## Model
- ta sama baza kodu co kanal Store
- aplikacja jest publikowana jako `unpackaged` Windows App SDK
- wynikowym artefaktem dystrybucyjnym jest standardowy instalator `EXE`
- instalator nie uruchamia pod spodem pakietu `MSIX`
- installer kopiuje pelny output `publish` do wybranego katalogu, domyslnie `Program Files\TyfloCentrum`
- installer dodaje skrot `TyfloCentrum` do menu Start i opcjonalnie na pulpit

## Co to daje
- standardowy instalator Windows uruchamiany bezposrednio z pliku `.exe`
- klasyczny wizard z wyborem katalogu instalacji
- brak zaleznosci od `Install.ps1`, `Add-AppxPackage` i tozsamosci pakietu
- ta sama logika produktu, widoki i uslugi aplikacji co w wersji Store

## Ograniczenia
- kanal `unpackaged` nie ma tozsamosci pakietu
- funkcje zalezne od package identity sa w nim celowo ograniczone:
  - `WNS`
  - systemowe powiadomienia App SDK
- aktualizacje tego kanalu trzeba dystrybuowac samodzielnie

## Jak zbudowac lokalnie

W PowerShell na Windows:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\Build-DirectSetupExe.ps1
```

Wynik:
- `artifacts\DirectSetup\TyfloCentrumSetup_Release_x64\TyfloCentrumSetup_<wersja>_x64.exe`

Skrypt:
- publikuje aplikacje jako `unpackaged`
- dopina brakujace artefakty `PRI/XBF`
- buduje standardowy instalator `EXE` przez `Inno Setup`

## Jak zbudowac przez GitHub Actions

Repo ma workflow:

- `Direct Installer Build`

Uruchomienie:
1. wejdz do `Actions`
2. wybierz `Direct Installer Build`
3. kliknij `Run workflow`
4. ustaw:
   - `git_ref`
   - `configuration`
   - `platform`

Workflow:
- uruchamia testy jednostkowe
- buduje `unpackaged publish`
- buduje `EXE`
- zapisuje gotowy plik `EXE` jako artefakt

## Wynik
- glowny plik:
  - `TyfloCentrumSetup_<wersja>_x64.exe`
- katalog roboczy:
  - `artifacts/DirectSetup/TyfloCentrumSetup_<Configuration>_<Platform>/`

## Powiazane dokumenty
- [README repo](../README.md)
- [Setup deweloperski](developer-setup.md)
