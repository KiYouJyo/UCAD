# UCAD v0.3.6 — Startup Reliability Hotfix

v0.3.6 is a reliability hotfix for the startup regression introduced with the v0.3.5 workspace shell. The new real startup smoke test and startup diagnostics identified the exact failure: the new UI called `ResourceLoader.GetString()` with a `.Text` resource key intended for XAML `x:Uid` property assignment. At runtime that lookup threw `0x80073B17 NamedResource Not Found`, terminating UCAD before any window became visible and making the app appear to do nothing when launched.

## Fixes

- Read the tool-shelf hint from the plain named resource `ToolShelfHint` instead of passing the XAML property resource key `ToolShelfHintText.Text` to `ResourceLoader.GetString()`.
- Add missing-runtime-resource protection and diagnostics to `GetString()` so a single localization-key failure cannot directly crash the entire app at startup; missing keys are recorded in the startup log.
- Defer creation of the first CAD workspace from the `MainWindow` constructor until the root visual tree has loaded, further reducing Win2D/workspace initialization risk during the earliest launch phase.
- Preserve the v0.3.5 multi-document architecture; `CadWorkspaceSession`, independent `CadDocument`, `CadViewport`, and `CommandSession` instances remain in place.
- Add startup diagnostics covering window construction, activation, initial workspace creation, and unhandled exceptions.
- Startup log location: `%LOCALAPPDATA%\UCAD\Logs\startup.log`.

## CI regression protection

The previous CI pipeline validated Core tests, WinUI compilation, and MSIX packaging, but it did not actually launch UCAD. That allowed a buildable and packageable app to pass while still failing immediately at runtime.

v0.3.6 adds and strengthens `startup-smoke` so that it:

- launches `UCAD.App.exe` on a Windows runner;
- waits eight seconds and verifies the process is still alive;
- prints `startup.log` and fails CI if the app exits early;
- rejects code-side `GetString()` calls that use `.Text`, `.Content`, or other `x:Uid` property-style keys instead of plain named resources;
- verifies every runtime resource key exists in the zh-CN, ja-JP, and en-US RESW sets;
- fails even if the process stays alive when the startup log reports `MissingResource`.

PR CI continues to require all of the following:

- Core tests
- WinUI app build
- Startup smoke / runtime localization contract
- MSIX / one-click package validation

The PR also publishes a temporary one-click acceptance package so the installed startup path can be manually verified before the hotfix is merged and released.

## Scope

This release is limited to startup reliability and the corresponding validation infrastructure. It does not change the v0.3.5 workspace UI, drawing commands, or CAD Core boundaries. Selection, OSNAP, Ortho, and other v0.4.0 interaction work remains on the existing roadmap.
