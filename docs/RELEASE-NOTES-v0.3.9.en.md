# UCAD v0.3.9 — UI Completion / Figma Fidelity / Start & Settings Foundation

v0.3.9 is a UI Foundation Completion release. It does not expand CAD Core; it brings the production WinUI 3 interface into alignment with the UCAD Figma visual SSOT and establishes a shell that can carry Selection / OSNAP / Inspector work without another structural rewrite.

## Highlights

- Rebuilt the browser-style title bar: UCAD Brand → continuous Document Tabs → `+` → drag region → native Windows caption buttons.
- Added explicit Drawing / Start / Settings Workspace Page types. `+` opens Start, and a real `CadWorkspaceSession` is created only after choosing New Drawing from Start.
- Completed the Start Center with New/Open entry points, an honest empty Recent state, Blank / Architecture / Urban Planning template information architecture, and Learn UCAD. Unsupported file I/O and templates are not presented as implemented features.
- Implemented all seven Settings areas: General, Appearance, Drafting, Input & Interaction, Files & Save, Language & Region, and About UCAD.
- Unified Settings to the Figma rhythm: 228 px navigation, 54 px content offset, 940×72 cards, and 35 / 12 / 8 / 30 px vertical spacing. The About application card is 940×128.
- Kept App Theme and CAD Canvas state independent. Canvas background, grid visibility/opacity, cursor-centered zoom, middle-button pan, and reverse wheel zoom are connected to the existing viewport behavior.
- Replaced generic UI placeholder glyphs with Fluent / WinUI icons; CAD-specific geometry uses PathIcon. Unicode fake icons are removed from production XAML.
- Added centralized `SettingsService` / `AppSettings` persistence at `%LOCALAPPDATA%\UCAD\settings.json` instead of scattering storage keys across views.
- Added complete zh-CN / ja-JP / en-US Start and Settings resource sets. Display-language preferences are applied before the next shell is created to avoid mixed-language partial refreshes.
- Made root `VERSION` the release version SSOT and aligned Assembly / UI / release metadata / MSIX Package to 0.3.9.
- Preserved PerMonitorV2 and XAML DIP behavior; no bitmap UI scaling was introduced.

## CAD Core

This release does not add Selection, OSNAP, Ortho, MOVE, COPY, TRIM, OFFSET, DWG/DXF, GIS, Architecture Objects, or Planning Objects.

Existing LINE / PLINE / RECTANGLE / CIRCLE / ARC / Undo / Redo / Clear / Reset View, multi-document sessions, command line, CommandRegistry, Zoom / Pan continue to use the existing `CommandRegistry → CommandSession → CAD Core` path.

## Validation

CI continues to cover Core tests, app-build, startup-smoke, and package-validation, and now also checks the Figma dimension/token contract, three-language resource parity, PerMonitorV2, version SSOT consistency, Unicode placeholder icon removal, and 1440×900 runtime screenshots for Drawing / Start / Settings General / Appearance / Input & Interaction / About.
