# UCAD v0.8.0 acceptance record

Date: 2026-08-18

## Frozen scope

UCAD v0.8.0 closes the post-v0.7 development branch as the Document & Exchange Foundation milestone. The accepted scope includes native document I/O, DXF exchange, extended 2D drawing/modify/annotation entities, layouts and plotting/PDF, architecture/planning helpers, GIS exchange, autosave/recovery foundations, and spatial indexing.

## Automated acceptance result

GitHub Actions run `32110136276` completed successfully with all required gates green:

- Core tests: 248 passed, 0 failed, 0 skipped.
- WinUI x64 Release build: succeeded with 0 warnings and 0 errors.
- UI and release contracts: passed.
- GitHub release signing secrets: validated.
- x64 MSIXBundle: built successfully.
- Release-certificate signing and signature verification: passed (`CN=AppPublisher`).
- One-click installer generation and repository package validation: passed.
- SHA-256 manifest: generated.
- Signed acceptance artifact: `UCAD-v0.8.0-signed-acceptance`.

## Acceptance package

The signed artifact contains:

- `UCAD_0.8.0.0_x64.msixbundle`
- `UCAD-v0.8.0-x64-one-click.zip`
- `UCAD-v0.8.0-release.cer`
- `SHA256SUMS.txt`

Artifact SHA-256 (GitHub Actions archive): `ad2721f9d012fd0a41813640ee0f9e6d8617f1815c2a4c42de4984faf6fe48b0`.

Package hashes recorded by the acceptance workflow:

- `UCAD_0.8.0.0_x64.msixbundle`: `68e038d416f75a3639a40d2e2234cef6e580cc52ad7410c6a846041b9f205d07`
- `UCAD-v0.8.0-x64-one-click.zip`: `4f78ac44fdd1e20ef965c88016c8776e111bf2a8dc3533a4e9f80024813c8092`

## Scope boundary

This milestone does not claim full DWG compatibility, 3D/BIM authoring, dynamic blocks, or full AutoCAD API compatibility.
