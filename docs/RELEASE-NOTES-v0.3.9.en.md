# UCAD v0.3.9 — UI Completion / Figma Fidelity / Start & Settings Foundation

v0.3.9 is a UI Foundation Completion release. It does not expand CAD Core; it aligns the WinUI 3 shell, Start, Settings, and page-transition behavior with the UCAD Figma visual SSOT and establishes a stable interface foundation for later Selection / OSNAP / Inspector work.

## Highlights

- Rebuilt the browser-style title bar: UCAD Brand → continuous Document Tabs → `+` → drag region → native Windows caption buttons.
- Added explicit Drawing / Start / Settings Workspace Page types. By default `+` opens Start; disabling “show Start on new tab” makes `+` create a blank Drawing directly.
- Completed the Start Center with New/Open entry points, an honest empty Recent state, Blank / Architecture / Urban Planning template information architecture, and Learn UCAD. Unsupported file, recent-file, and professional-template features are not presented as implemented behavior.
- Implemented all seven Settings areas: General, Appearance, Drafting, Input & Interaction, Files & Save, Language & Region, and About UCAD.
- Unified Settings to the Figma rhythm: 228 px navigation, 54 px content offset, 940×72 cards, and 35 / 12 / 8 / 30 px vertical spacing. The About application card is 940×128.
- Made App Theme and CAD Canvas Theme independently effective at runtime. App Theme changes the shell/native-control palette; Canvas Theme independently controls entity, transient-preview, grid, and crosshair colors while Canvas Background remains separate.
- Connected canvas background, grid visibility/opacity, cursor-centered zoom, middle-button pan, reverse wheel zoom, coordinate precision, and decimal formatting to real runtime behavior.
- Disabled or clearly reserved unsupported Restore Session, manual UI Scale, automatic update checking, and recent-history clearing rather than fabricating backend behavior.
- Replaced generic UI placeholder glyphs with Fluent / WinUI icons; CAD-specific geometry uses PathIcon. Unicode fake icons are removed from production XAML.
- Added centralized `SettingsService` / `AppSettings` persistence at `%LOCALAPPDATA%\UCAD\settings.json` instead of scattering storage keys across views.
- Added complete zh-CN / ja-JP / en-US Start and Settings resource sets. Display-language preference is applied before the next shell is created, avoiding a partially refreshed mixed-language session.
- Made root `VERSION` the release version SSOT and aligned Assembly / UI / release metadata / MSIX Package to 0.3.9.
- Preserved PerMonitorV2 and XAML DIP behavior; no bitmap UI scaling was introduced.

## CAD Core

This release does not add Selection, OSNAP, Ortho, MOVE, COPY, TRIM, OFFSET, DWG/DXF, GIS, Architecture Objects, or Planning Objects.

Existing LINE / PLINE / RECTANGLE / CIRCLE / ARC / Undo / Redo / Clear / Reset View, multi-document sessions, command line, CommandRegistry, Zoom / Pan continue to use the existing `CommandRegistry → CommandSession → CAD Core` path.

## Validation

Required CI covers:

- Core tests;
- app-build;
- startup-smoke with a real UCAD launch and Start / Settings initialization;
- package-validation and one-click package validation;
- Figma key dimension/color and Design Token contracts;
- Start / Settings / Canvas behavior contracts;
- three-language resource-key parity;
- PerMonitorV2;
- version SSOT consistency;
- Unicode placeholder-icon removal.

Pixel-level 1440×900 Figma comparison remains a manual `UI Fidelity Screenshots` workflow. It runs only when the runner provides a real 1440×900 interactive desktop, so hosted-desktop limitations do not block functional acceptance or the production release.