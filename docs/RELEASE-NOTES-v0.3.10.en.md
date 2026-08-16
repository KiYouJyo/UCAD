# UCAD v0.3.10 — Live Trilingual Localization Hotfix

v0.3.10 fixes two localization regressions from v0.3.9: Start / Settings could display resource identifiers instead of translated text, and changing the display language required a restart. This release adds no CAD Core capability.

## Fixes

- Fixed Start / Settings showing identifiers such as `Start_TabTitle` and `Settings_Nav_Title` instead of real UI strings.
- Load `UcadV039.resw` through the Windows App SDK named-resource-map pattern using the default PRI path plus the `UcadV039` ResourceMap.
- Added a centralized `LocalizationService` for the zh-CN / ja-JP / en-US runtime resource context.
- Added **restart-free** switching between Simplified Chinese, Japanese, and English. The current Window, Start, Settings, document tabs, menus, category bar, Inspector, command area, and status bar are relocalized in place.
- Language changes do not recreate the Window or existing `CadWorkspaceSession` objects, so geometry, Undo/Redo history, viewport state, and multi-document sessions remain intact.
- Preserved Follow System Language; when disabled, zh-CN / ja-JP / en-US can be selected and applied immediately.
- Untitled document labels relocalize in place, for example `图纸 1 / 図面 1 / Drawing 1`.
- Updated language-setting guidance so the UI no longer claims that a restart is required.

## Validation

A dedicated Localization Smoke now switches `zh-CN → ja-JP → en-US` inside **one running UCAD process** and verifies representative Start, Settings, and Shell resources resolve to translated text rather than identifiers.

Existing Core tests, app-build, startup-smoke, package-validation, version SSOT, PerMonitorV2, and three-language resource-key parity remain required.
