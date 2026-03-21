# Agent instructions (scope: this directory and subdirectories)

## Scope and layout
- This AGENTS.md applies to: `tests/` and below.
- This directory owns automated verification for unit, contract, smoke, and future UI automation tests.

## Conventions
- Keep unit tests deterministic and independent from network access.
- Prefer fixture-driven tests for API contracts.
- Mark Windows-only tests clearly.
- Accessibility-critical flows should have either:
  - automated smoke coverage
  - or a linked manual verification checklist in docs

## Test strategy
- `Tyflocentrum.Windows.Tests/`
  - domain
  - infrastructure
  - parser and policy tests
- `Tyflocentrum.Windows.UITests/`
  - shell smoke
  - navigation smoke
  - future accessibility automation helpers

## Do not
- Do not hit real production endpoints in automated tests.
- Do not add flaky timing-dependent UI tests when a ViewModel test would cover the behavior.

