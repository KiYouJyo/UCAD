# UCAD v0.9.3

This release consolidates the AutoCAD interoperability foundation, command-entry ergonomics, trilingual UI completion, and a GitHub-backed in-app update path.

## AutoCAD interoperability

- Established format boundaries for DWG / DWT / DWS / DXF / DXB and expanded high-value 2D entities, text, dimensions, blocks, hatches, layouts, and viewports.
- Added a pinned real-file regression corpus spanning major DWG generations from AutoCAD R14 through 2018+, plus R12 / 2000 / 2018+ DXF fixtures.
- Added a deterministic 12,000-entity complex drawing regression covering major entities, layers, blocks, dimension styles, and paper-layout state across DWG round trips.
- For ObjectARX / Proxy / custom objects whose editability cannot be proven, UCAD conservatively preserves the original AutoCAD container. Untouched drawings can be emitted byte-for-byte, and the original source remains recoverable after edits.

## Command input and dynamic display

- Opening or switching to a drawing now automatically focuses the bottom command input so CAD commands can be typed immediately.
- Added an AutoCAD-style cursor-local command display beside the central pickbox. It follows the pointer and automatically flips away from viewport edges.
- The dynamic panel is display-only; the bottom command line remains the sole text/IME owner, avoiding Chinese and Japanese IME conflicts.
- CAD-style Delete behavior is preserved: with an empty command line it erases selected geometry, while typed command text keeps normal text-editing Delete behavior.

## Simplified Chinese / Japanese / English

- Audited and completed zh-CN / ja-JP / en-US coverage across Start, shell, Settings, dynamically generated toolbars, and command tools.
- Fixed tool shelves that could remain in English until first expanded, and validated live language switching in a single running process.
- Added strict resource-key parity tests and runtime localization smoke coverage to catch future language omissions.

## In-app updates

- Settings > General > Check for updates is now connected to GitHub Releases instead of a placeholder.
- Optional automatic update checks can run after startup without blocking the main UI.
- Only stable, non-draft releases with semantic `vX.Y.Z` tags are accepted.
- UCAD downloads the x64 `.msixbundle` into its local update cache and validates both the GitHub asset length and the SHA-256 published in `SHA256SUMS.txt`.
- After verification, installation is handed to Windows App Installer. UCAD does not silently replace installed files; installation remains user-authorized.
- Update checks, download progress, and errors are fully localized in all three supported languages.

## Known boundaries

- Unknown ObjectARX / Proxy / custom payloads are not yet re-injected into a rebuilt DWG after editing; original-source recovery remains available.
- Radius/diameter/ordinate dimensions, richer MLEADER, dynamic blocks, complex HATCH boundaries, and DXF layout objects remain future interoperability work.
- DWF/DWFx, CTB/STB/PC3/PMP, DST/DSD, and other peripheral AutoCAD ecosystem formats remain on the roadmap.
