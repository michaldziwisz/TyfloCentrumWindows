# Agent instructions (scope: this directory and subdirectories)

## Scope and layout
- This AGENTS.md applies to: repo root and everything below it.
- Current state: the repo has documentation and an initial scaffolding phase in progress.
- Primary source of truth is the documentation under `docs/`, but implementation files now live under `src/` and `tests/`.

## Modules / subprojects

| Module | Type | Path | What it owns | How to run | Tests | Docs | AGENTS |
|--------|------|------|--------------|------------|-------|------|--------|
| docs | docs | `docs/` | product scope, architecture, a11y, release, ADRs, backlog | n/a | manual review | `docs/index.md` | `docs/AGENTS.md` |
| app | app | `src/Tyflocentrum.Windows.App/` | app bootstrap, shell, packaged entrypoint, app assets | open in Visual Studio on Windows | future solution build | `docs/architecture.md` | `src/AGENTS.md` |
| ui | library | `src/Tyflocentrum.Windows.UI/` | viewmodels, UI registrations, future shared controls and feature views | open in Visual Studio on Windows | future solution build | `docs/accessibility-requirements.md` | `src/AGENTS.md` |
| domain | library | `src/Tyflocentrum.Windows.Domain/` | domain models, contracts, app section catalog, future policies | open in Visual Studio on Windows | future solution build | `docs/product-requirements.md` | `src/AGENTS.md` |
| infrastructure | library | `src/Tyflocentrum.Windows.Infrastructure/` | endpoints, storage adapters, service registration, future HTTP/audio integrations | open in Visual Studio on Windows | future solution build | `docs/api-integrations.md` | `src/AGENTS.md` |
| tests | tests | `tests/` | unit tests and future UI automation scaffolding | open in Visual Studio on Windows | future solution build | `docs/testing-strategy.md` | `tests/AGENTS.md` |

## Cross-domain workflows
- Windows app -> WordPress API:
  - `tyflopodcast.net/wp-json`
  - `tyfloswiat.pl/wp-json`
- Windows app -> contact panel:
  - `kontakt.tyflopodcast.net/json.php`
- Windows app -> optional push service:
  - `tyflocentrum.tyflo.eu.org`
- Source-analysis workflow:
  - if behavior parity is unclear, inspect `/mnt/d/projekty/tyflocentrum/` before inventing a Windows-specific behavior

## Verification (preferred commands)
- During the current docs-first phase:
  - verify links, paths, naming consistency, and doc cross-references
- Once code exists:
  - prefer module-local build and test commands
  - run quiet first, then rerun narrowed failures with more detail

## Docs usage
- In this repo stage, read `docs/index.md` first before making structural changes.
- For implementation tasks, consult only the docs relevant to the area you are changing:
  - architecture
  - accessibility
  - API integrations
  - testing
  - release

## Global conventions
- Keep the Windows implementation native:
  - `WinUI 3`
  - `Windows App SDK`
  - `.NET 8`
  - packaged app with `MSIX`
- Accessibility is a release requirement, not a polish task.
- Prefer native XAML controls over custom controls.
- If a custom control is necessary, document and implement its UI Automation behavior explicitly.
- Do not change the target stack, packaging model, or accessibility support matrix without updating docs and adding an ADR.
- Keep docs in sync with code. Any meaningful architecture or release change updates `docs/`.
- Preserve a clear split between:
  - UI
  - domain logic
  - infrastructure
  - tests

## Do not
- Do not introduce `MAUI`, Electron, Tauri, Avalonia, or web-wrapper alternatives without an ADR and explicit user approval.
- Do not assume iOS-specific interactions must be ported 1:1 to Windows.
- Do not create code directly in repo root once the solution is scaffolded.
- Do not treat screen-reader behavior as implied. Document observable expectations.

## Links to module instructions
- `docs/AGENTS.md`
- `src/AGENTS.md`
- `tests/AGENTS.md`
