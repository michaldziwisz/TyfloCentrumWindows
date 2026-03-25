# Pierwsza publikacja w Partner Center

## Cel
Przejsc pierwszy release `TyfloCentrum` do Microsoft Store tak, aby:
- pierwsza submission przeszla recznie w Partner Center
- po niej mozna bylo bezpiecznie przejsc na workflow GitHub Actions

## Stan repo na dzis
Repo ma juz przygotowane:
- build `MSIX` i `.appxsym`
- workflow GitHub Actions do draftu, release i metadata submission
- branding `TyfloCentrum`

Do pierwszej publikacji nadal potrzebny jest reczny setup w Partner Center.

## Rzeczy do zapisania podczas setupu
Zanim zaczniesz, przygotuj miejsce na zapisanie:
- `STORE_PRODUCT_ID`
- finalnej wartosci `Package/Identity Name`
- finalnej wartosci `Publisher`
- finalnej wartosci `PublisherDisplayName`
- adresu URL polityki prywatnosci
- adresu supportowego lub strony produktu

Te wartosci beda potem potrzebne do repo i GitHub Actions.

## Checklista

### 1. Utworz aplikacje w Partner Center
- Zaloguj sie do Partner Center i wybierz utworzenie nowej aplikacji.
- Zarezerwuj nazwe `TyfloCentrum`.
- Zapisz `Store product ID`.

### 2. Skonfiguruj tozsamosc pakietu
- Otworz strone `Product identity` w Partner Center.
- Skopiuj wartosci tozsamosci Store do repo.
- Zmien w [Package.appxmanifest](/mnt/d/projekty/tyflocentrum_pc/src/TyfloCentrum.Windows.App/Package.appxmanifest):
  - `Identity Name`
  - `Publisher`
  - `PublisherDisplayName`
- Nie publikuj do Store z przypadkowa lub tymczasowa tozsamoscia developerska.

Uwaga:
- obecnie repo ma lokalna tozsamosc developerska `TyfloCentrum.Windows` / `CN=TyfloCentrum`
- przed pierwsza submission musisz ja uzgodnic z wartosciami z Partner Center

### 3. Uzupelnij listing sklepu
- Dodaj nazwe produktu, opis krotki i opis pelny.
- Dodaj screenshoty dla `pl-PL`.
- Dodaj kategorie i slowa kluczowe, jesli Partner Center ich wymaga dla wybranego typu wpisu.
- Uzupelnij dane supportowe.
- Dodaj URL polityki prywatnosci.
- Jesli certyfikacja wykryje zaleznosc od zewnetrznego oprogramowania, ujawnij ja w pierwszych dwoch liniach opisu aplikacji.

Na potrzeby pierwszej submission mozna uzyc strony z repo po wdrozeniu GitHub Pages:
- polityka prywatnosci: `https://michaldziwisz.github.io/TyfloCentrumWindows/privacy/`
- strona glowna/support: `https://michaldziwisz.github.io/TyfloCentrumWindows/`

Praktycznie dla `TyfloCentrum` warto przygotowac od razu:
- opis funkcji: aktualnosci, podcasty, artykuly, Tyfloradio, glosowki
- informacje o dostepnosci: obsluga klawiatury, NVDA, Narrator
- minimum jeden komplet aktualnych screenshotow z aplikacji
- jesli Store zglosi zaleznosc `.NET Desktop Runtime`, zacznij opis od zdania:
  - `TyfloCentrum wymaga do dzialania Microsoft .NET Desktop Runtime.`

### 4. Ustaw dostepnosc rynkowa i model biznesowy
- Ustaw produkt jako bezplatny.
- Ustaw rynki, w ktorych aplikacja ma byc dostepna.
- Ustaw widocznosc wydania zgodnie z planem: publiczna albo ograniczona, jesli chcesz najpierw testowac.

To jest wazne, bo aktualna automatyzacja GitHub Actions oparta o Microsoft Store CLI wspiera produkty bezplatne.

### 5. Wypelnij ankiete wiekowa i deklaracje tresci
- Wypelnij age rating questionnaire.
- Sprawdz, czy tresc aplikacji nie wymaga dodatkowych deklaracji.
- Upewnij sie, ze opisy i listing sa zgodne z odpowiedziami z ankiety.

### 6. Zweryfikuj capabilities i opis funkcji
- Potwierdz, ze listing i notatki do certyfikacji wyjasniaja uzycie:
  - internetu
  - mikrofonu
- Jesli opisujesz glosowki albo Tyfloradio, zaznacz po co aplikacja potrzebuje mikrofonu.

### 7. Przygotuj pierwszy pakiet release
- Zwieksz wersje aplikacji do pierwszej wersji publikacyjnej.
- Zbuduj release `MSIX`.
- Zachowaj `.appxsym`.
- Przetestuj instalacje, uruchomienie, odinstalowanie i aktualizacje lokalnie.

W tym repo podstawowy build wykonasz przez:
```powershell
cd D:\projekty\tyflocentrum_pc
powershell -ExecutionPolicy Bypass -File .\scripts\windows\Build-DevMsix.ps1 -Configuration Release
```

### 8. Zrob pierwsza reczna submission
- W Partner Center utworz pierwsza submission.
- Wgraj paczke aplikacji.
- Uzupelnij listing, polityke prywatnosci, age ratings i pozostale sekcje.
- Dodaj notatki dla certyfikacji, jesli sa potrzebne.
- Wyslij submission do review.

Ta pierwsza submission jest potrzebna, zanim zaczniesz sensownie korzystac z automatyzacji metadata i kolejnych publikacji z GitHub Actions.

### 9. Skonfiguruj Entra ID i API do automatyzacji
- Skojarz Partner Center z Microsoft Entra ID.
- Zarejestruj aplikacje Entra ID do Store API / CLI.
- Nadaj tej aplikacji odpowiednia role w Partner Center.
- Zapisz dane:
  - `AZURE_AD_APPLICATION_CLIENT_ID`
  - `AZURE_AD_APPLICATION_SECRET`
  - `AZURE_AD_TENANT_ID`
  - `SELLER_ID`

### 10. Dodaj konfiguracje do GitHub
- Dodaj repository secrets:
  - `AZURE_AD_APPLICATION_CLIENT_ID`
  - `AZURE_AD_APPLICATION_SECRET`
  - `AZURE_AD_TENANT_ID`
  - `SELLER_ID`
- Dodaj repository variable:
  - `STORE_PRODUCT_ID`

### 11. Zrob pierwszy test automatyzacji
- Uruchom `Store Get Base Metadata`.
- Pobierz artefakt i zapisz go jako `store/metadata/submission.json`.
- Uruchom `Store Update Metadata`, jesli chcesz przeniesc listing do repo.
- Uruchom `Store Draft Submission`.
- Zweryfikuj draft w Partner Center.

### 12. Dopiero potem publikuj z workflow release
- Uzyj `Store Release Submission` dla normalnych wydan.
- Jesli chcesz, zacznij od rollout procentowego.
- Zachowuj `.appxsym` do diagnostyki crashy.

## Minimalna checklista przed kliknieciem Submit
- tozsamosc pakietu w repo zgadza sie z `Product identity`
- numer wersji jest zwiekszony
- listing ma finalne teksty po polsku
- screenshoty sa aktualne
- polityka prywatnosci jest publicznie dostepna
- age rating questionnaire jest wypelniony
- capabilities sa uzasadnione w opisie i notatkach
- paczka `MSIX` uruchamia sie poprawnie lokalnie
- masz zapisane `STORE_PRODUCT_ID`, `SELLER_ID` i dane Entra ID

## Co warto zrobic od razu po pierwszej akceptacji
- uruchomic `Store Get Base Metadata`
- zapisac metadata submission w repo
- wlaczyc workflow Store jako standardowy proces release
- przygotowac procedure podnoszenia wersji aplikacji przed kazda submission

## Dokumenty powiazane
- [Pakowanie MSIX i Microsoft Store](release-msix-store.md)
- [Microsoft Store przez GitHub Actions](store-publishing-github-actions.md)

## Zrodla
- Microsoft Store CLI overview: https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/overview
- Microsoft Store CLI commands: https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/commands
- GitHub Actions for Microsoft Store CLI: https://learn.microsoft.com/en-us/windows/apps/publish/msstore-dev-cli/github-actions
- Microsoft Store submission API: https://learn.microsoft.com/en-us/windows/apps/publish/store-submission-api
- Package and upload apps to Partner Center: https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/upload-app-packages
