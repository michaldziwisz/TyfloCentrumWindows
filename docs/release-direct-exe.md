# Dystrybucja poza Store jako direct EXE

## Cel
Utrzymac drugi kanal dystrybucji dla TyfloCentrum poza Microsoft Store, bez rozdzielania bazy kodu aplikacji.

## Model
- ta sama baza kodu co kanal Store
- artefakt wynikowy: instalator `EXE`
- instalator korzysta z dzialajacego pakietu `MSIX`
- instalator zawiera kompletny payload `MSIX` z zaleznosciami i skryptami sideload jako archiwum rozpakowywane do katalogu tymczasowego
- uzytkownik nie musi korzystac ze Store

## Co to daje
- uzytkownik moze zainstalowac aplikacje poza Microsoft Store z pojedynczego pliku `EXE`
- nie trzeba utrzymywac oddzielnego projektu UI ani oddzielnej logiki domenowej
- Store i direct EXE moga byc budowane z tego samego repo

## Ograniczenia
- to nie jest kanal zarzadzany przez Store
- aktualizacje trzeba dystrybuowac samodzielnie
- pod spodem instalator nadal rozpakowuje i instaluje `MSIX`
- niesygnowany `EXE` nadal moze pokazac ostrzezenie `SmartScreen`
- bootstrap instalatora zapisuje tez prosty log diagnostyczny do `%TEMP%\TyfloCentrumSetup.log`
- jesli pozniej pojawi sie podpisany kanal direct, ten sam model mozna przepiac na certyfikat produkcyjny

## Jak zbudowac direct EXE

W PowerShell na Windows:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\Build-DirectSetupExe.ps1
```

Opcjonalnie dla `ARM64`:

```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\Build-DirectSetupExe.ps1 -Platform ARM64
```

## Jak zbudowac direct EXE przez GitHub Actions

Repo ma tez osobny workflow:

- `Direct EXE Build`

Uruchomienie:

1. wejdz do `Actions` w repo GitHub
2. wybierz `Direct EXE Build`
3. kliknij `Run workflow`
4. ustaw:
   - `git_ref`
   - `configuration`
   - `platform`

Workflow:

- uruchamia testy jednostkowe
- buduje pakiet `MSIX`
- pakuje go do instalatora `EXE`
- zapisuje gotowy instalator i jego `.zip` jako GitHub artifact

## Wynik

Artefakt jest publikowany do:

- `artifacts/DirectSetup/TyfloCentrumSetup_Release_x64/`
- albo odpowiednio dla `ARM64`

Glowny plik wynikowy:

- `TyfloCentrumSetup_x64.exe`

## Zasady utrzymania
- nie rozdzielac logiki produktu miedzy Store i direct EXE bez realnej potrzeby
- zmiany w procesie dystrybucji aktualizuja tez:
  - `README.md`
  - `docs/developer-setup.md`
  - `docs/release-msix-store.md`
  - workflow `Direct EXE Build`
