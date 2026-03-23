# Push Service dla Windows

## Cel
Ten dokument opisuje osobny backend `TyfloCentrum.PushService`, dodany do repozytorium Windows po to, aby mozna bylo wdrazac powiadomienia `WNS` niezaleznie od starego `push-service` z aplikacji iOS.

## Zakres
Serwis obsluguje:
- `GET /health`
- `POST /api/v1/register`
- `POST /api/v1/update`
- `POST /api/v1/unregister`
- `POST /api/v1/events/live-start`
- `POST /api/v1/events/live-end`
- `POST /api/v1/events/schedule-updated`
- polling `WordPress` dla nowych:
  - podcastow
  - artykulow
- realna wysylke toastow do `WNS`

## Projekt
- kod: `src/TyfloCentrum.PushService/`
- testy: `tests/TyfloCentrum.PushService.Tests/`

## Kontrakt rejestracji klienta

### `POST /api/v1/register`
Body:
- `token`
- `env`
- `prefs`

Przyklad:
```json
{
  "token": "https://bn1.notify.windows.com/?token=...",
  "env": "windows-wns",
  "prefs": {
    "podcast": true,
    "article": true,
    "live": false,
    "schedule": false
  }
}
```

### `POST /api/v1/update`
Body:
- `token`
- `prefs`

### `POST /api/v1/unregister`
Body:
- `token`

## Konfiguracja
Sekcja `PushService` w `appsettings.json`:
- `DataDirectory`
- `StateFileName`
- `TokenTtlDays`
- `MaxTokens`
- `PollIntervalSeconds`
- `PollPerPage`
- `WebhookSecret`
- `TyflopodcastWordPressBaseUrl`
- `TyfloswiatWordPressBaseUrl`
- `AzureTenantId`
- `AzureClientId`
- `AzureClientSecret`
- `WnsScope`

## Sekrety wymagane do WNS
Do realnej wysylki przy zamknietej aplikacji wymagane sa:
- `AzureTenantId`
- `AzureClientId`
- `AzureClientSecret`
- mapowanie `PFN -> Azure App ID` po stronie Microsoft
- zgodna konfiguracja klienta Windows:
  - `PushAzureAppId`
  - `PushAzureObjectId`

Bez tych danych serwis:
- dalej przyjmuje rejestracje
- dalej polluje WordPress
- ale nie wysle realnie toastow do `WNS`

## Model wdrozenia na VPS
Rekomendowany model:
- katalog aplikacji: `/opt/tyflocentrum-push-windows`
- dane: `/var/lib/tyflocentrum-push-windows`
- systemd unit: `tyflocentrum-push-windows.service`
- reverse proxy przez nginx na osobnym subdomenowym hoscie lub pod osobna sciezka

Przykladowe pliki:
- `scripts/linux/push-service/tyflocentrum-push-windows.service.example`
- `scripts/linux/push-service/nginx.tyflocentrum-push-windows.conf.example`

## Build i publish
Lokalnie:
```powershell
dotnet publish .\src\TyfloCentrum.PushService\TyfloCentrum.PushService.csproj -c Release -o .\artifacts\publish\TyfloCentrum.PushService
```

Na Linuxie:
```bash
dotnet /opt/tyflocentrum-push-windows/TyfloCentrum.PushService.dll
```

## Wymagania sieciowe
Serwis musi miec wyjscie do:
- `login.microsoftonline.com`
- `wns.windows.com`
- `tyflopodcast.net`
- `tyfloswiat.pl`

## Zachowanie pollingu
- pierwszy start ustawia baseline i nie wysyla zaleglych powiadomien
- kolejne iteracje wysylaja tylko nowe wpisy
- uszkodzone albo wygasle kanaly `WNS` sa usuwane po odpowiedzi `404/410`

## Zrodla
- Microsoft Learn: Windows App SDK push quickstart  
  https://learn.microsoft.com/en-us/windows/apps/develop/notifications/push-notifications/push-quickstart
- Microsoft Learn: app notifications quickstart  
  https://learn.microsoft.com/pl-pl/windows/apps/develop/notifications/app-notifications/app-notifications-quickstart
