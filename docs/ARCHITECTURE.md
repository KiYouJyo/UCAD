# Architecture

UCAD is intentionally split into a UI-independent CAD core and a Windows-native presentation layer.

## `UCAD.Core`

Owns geometry, document state, entity models, command vocabulary, command-session state, and parsers that can be unit-tested without WinUI.

Current command foundation:

- `CadCommandDefinition` — canonical command name plus aliases.
- `CommandRegistry` — case-insensitive command resolution and duplicate-token protection.
- `CommandSession` — active/previous command lifecycle, repeat, complete, cancel.
- `CommandInputParser` — numeric, absolute coordinate, and relative coordinate parsing.

## `UCAD.App`

Owns WinUI 3 windowing, the Win2D viewport, keyboard routing, localized command prompts, pointer interaction, and MSIX integration.

The command line is deliberately thin: it resolves commands through `UCAD.Core`, then delegates drawing interaction to `CadViewport`. Mouse clicks and typed coordinates converge on the same point-submission path so later drawing commands can share one interaction model.

## Rendering

`CadViewport` performs world/screen transforms, adaptive grid rendering, crosshair drawing, zoom/pan, entity rendering, and transient previews. Geometry and command parsing must not depend on Win2D types.

## Boundary rule

Architecture/GIS helpers remain out of scope until the core drawing/editing loop reaches the v0.5 gate.
