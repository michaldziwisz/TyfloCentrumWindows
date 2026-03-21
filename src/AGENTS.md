# Agent instructions (scope: this directory and subdirectories)

## Scope and layout
- This AGENTS.md applies to: `src/` and below.
- This directory owns implementation code for the Windows application.

## Modules
- `TyfloCentrum.Windows.App/`
  - bootstrap
  - packaged app entrypoint
  - shell host
- `TyfloCentrum.Windows.UI/`
  - viewmodels
  - UI-specific registrations
  - future shared controls and feature views
- `TyfloCentrum.Windows.Domain/`
  - domain models
  - contracts
  - policies
- `TyfloCentrum.Windows.Infrastructure/`
  - HTTP
  - storage
  - Windows-specific adapters

## Conventions
- Keep `App` thin. Put business logic outside the app bootstrap layer.
- Prefer constructor injection over service location.
- Do not put product logic into XAML code-behind unless it is directly UI-event plumbing.
- Keep accessibility behavior explicit:
  - focus
  - automation names
  - state announcements
- Before adding a new dependency, check whether the functionality can be covered by:
  - .NET
  - Windows App SDK
  - CommunityToolkit

## Verification
- Once a Windows environment is available, verify from the solution root:
  - restore
  - build
  - unit tests
  - packaged app launch
- For UI changes, always add or update a manual accessibility verification note.

## Do not
- Do not mix transport DTOs with UI-only models unless there is a clear reason.
- Do not move stack or packaging decisions out of sync with `docs/`.
- Do not add custom controls before exhausting native XAML controls.

