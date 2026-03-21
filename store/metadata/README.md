# Store Metadata

Ten katalog jest przeznaczony na lokalne pliki JSON z metadata submission do Microsoft Store.

Rekomendowany flow:
- uruchom workflow `Store Get Base Metadata`
- pobierz artefakt `store-base-metadata`
- skopiuj JSON do `store/metadata/submission.json`
- wprowadz zmiany listingow i metadata
- uruchom workflow `Store Update Metadata`

Uwaga:
- struktura JSON powinna pochodzic z aktualnego `msstore submission get`
- nie edytuj metadata "w ciemno" bez pobrania aktualnej wersji z Partner Center
