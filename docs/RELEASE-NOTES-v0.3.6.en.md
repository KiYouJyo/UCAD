# UCAD v0.3.6 — Startup Reliability Hotfix

v0.3.6 is a reliability hotfix for the startup regression introduced with the v0.3.5 workspace shell. v0.3.5 moved UCAD to real multi-document sessions, but the first `CadWorkspaceSession` was created inside the `MainWindow` constructor, which also instantiated a Win2D-backed `CadViewport` before the window was shown. On some installed environments, that startup path could terminate the app before any window became visible, making UCAD appear to do nothing when launched.

## Fixes

- Defer creation of the first CAD workspace from the `MainWindow` constructor until the root visual tree has loaded.
- Preserve the v0.3.5 multi-document architecture; `CadWorkspaceSession`, independent `CadDocument`, `CadViewport`, and `CommandSession` instances remain in place.
- Add startup diagnostics covering window construction, activation, initial workspace creation, and unhandled exceptions.
- Startup log location: `%LOCALAPPDATA%\UCAD\Logs\startup.log`.

## CI regression protection

The previous CI pipeline validated Core tests, WinUI compilation, and MSIX packaging, but it did not actually launch UCAD. That allowed a buildable and packageable app to pass while still failing immediately at runtime.

v0.3.6 adds a `startup-smoke` job that:

- launches `UCAD.App.exe` on a Windows runner;
- waits eight seconds and verifies the process is still alive;
- prints `startup.log` and fails CI if the app exits early.

PR CI continues to require all of the following:

- Core tests
- WinUI app build
- Startup smoke
- MSIX / one-click package validation

## Scope

This release is limited to startup reliability and the corresponding validation infrastructure. It does not change the v0.3.5 workspace UI, drawing commands, or CAD Core boundaries. Selection, OSNAP, Ortho, and other v0.4.0 interaction work remains on the existing roadmap.
