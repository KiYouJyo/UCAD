# UCAD Architecture

UCAD separates CAD state from the Windows UI so that geometry, document behavior, commands, rendering, and file I/O can evolve independently.

```text
UCAD.Core          geometry, entities, document state
UCAD.Commands      planned command state machine and aliases
UCAD.Render        Win2D / future Direct2D rendering boundary
UCAD.App           WinUI 3 shell, packaged identity, localization
UCAD.IO            planned DXF-first import/export boundary
```

## Principles

1. The CAD model must not depend on XAML controls.
2. The viewport uses immediate-mode GPU rendering rather than one XAML element per CAD entity.
3. Commands should be testable without a window.
4. DXF is the first interchange target; DWG support must not block the core roadmap.
5. Packaging, signing, release metadata, and installer behavior are automated and validated in CI.
