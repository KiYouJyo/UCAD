# Contributing to UCAD

UCAD is in an early architecture phase. Small, reviewable changes are preferred over broad rewrites.

## Development rules

1. Keep CAD data and geometry out of the WinUI view layer.
2. Add deterministic tests for geometry/document behavior.
3. Do not add a new user-facing feature without defining its command/interaction behavior.
4. Avoid introducing cloud dependencies for core drafting functions.
5. Preserve source provenance for third-party or adapted code.

## Pull requests

- Create a focused branch.
- Keep commits scoped and descriptive.
- Run the core tests before opening a PR.
- Explain user-visible changes and architectural impact.
- Add/update `CHANGELOG.md` for notable changes.

## Third-party GPL code

Do not copy code from LibreCAD or another project without recording its source file/project, upstream license, and the adaptation made in UCAD. Attribution and license compatibility are release blockers.
