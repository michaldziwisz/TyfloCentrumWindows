# Dystrybucja poza Store jako direct MSI

## Cel
Utrzymac niezalezny od `MSIX` kanal instalacji dla uzytkownikow, ktorzy nie chca korzystac ze Store i nie powinni byc blokowani przez skrypty `PowerShell` instalujace pakiet AppX.

## Model
- ta sama baza kodu co kanal Store
- aplikacja jest publikowana jako `unpackaged` Windows App SDK
- wynikowym artefaktem dystrybucyjnym jest standardowy instalator `MSI`
- `MSI` nie instaluje pod spodem pakietu `MSIX`
- `MSI` kopiuje pelny output `publish` do `Program Files\TyfloCentrum`
- `MSI` dodaje tez skrot `TyfloCentrum` do menu Start

## Co to daje
- standardowy instalator Windows uruchamiany bezposrednio z pliku `.msi`
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
powershell -ExecutionPolicy Bypass -File .\scripts\windows\Build-DirectMsi.ps1
```

Wynik:
- `artifacts\DirectMsi\TyfloCentrumMsi_Release_x64\TyfloCentrumSetup_<wersja>_x64.msi`

Skrypt:
- publikuje aplikacje jako `unpackaged`
- dopina brakujace artefakty `PRI/XBF`
- buduje standardowy instalator `MSI` przez `WiX`

## Jak zbudowac przez GitHub Actions

Repo ma workflow:

- `Direct MSI Build`

Uruchomienie:
1. wejdz do `Actions`
2. wybierz `Direct MSI Build`
3. kliknij `Run workflow`
4. ustaw:
   - `git_ref`
   - `configuration`
   - `platform`

Workflow:
- uruchamia testy jednostkowe
- buduje `unpackaged publish`
- buduje `MSI`
- zapisuje gotowy plik `MSI` jako artefakt

## Wynik
- glowny plik:
  - `TyfloCentrumSetup_<wersja>_x64.msi`
- katalog roboczy:
  - `artifacts/DirectMsi/TyfloCentrumMsi_<Configuration>_<Platform>/`

## Powiazane dokumenty
- [README repo](../README.md)
- [Setup deweloperski](developer-setup.md)
