# Agent instructions (scope: this directory and subdirectories)

## Scope and layout
- This AGENTS.md applies to: `docs/` and below.
- This directory owns product and engineering documentation for the Windows project.

## Key directories
- `docs/index.md` - documentation entrypoint
- `docs/adr/` - architecture decision records
- `docs/windows-plan-and-backlog.md` - detailed backlog and rollout plan

## Conventions
- Keep docs concise, specific, and implementation-facing.
- Prefer Polish for project documentation because the product context and source repo documentation are already in Polish.
- Keep headings stable unless there is a strong reason to rename them.
- Use relative links between docs files.
- When adding a new significant document, update `docs/index.md`.
- When changing stack, packaging, testing strategy, or accessibility rules, update the corresponding ADR or add a new one.
- Document observable accessibility behavior, not just generic compliance claims.

## Common pitfalls
- Do not duplicate the same long explanation across many files. Link to the owning document.
- Do not let roadmap, backlog, and architecture drift apart.
- Do not record speculative implementation details as if they were already decided.

## Do not
- Do not treat this folder as a dump of notes. Every file should have a clear owner topic.
- Do not introduce new top-level docs if the content belongs in an existing file.

