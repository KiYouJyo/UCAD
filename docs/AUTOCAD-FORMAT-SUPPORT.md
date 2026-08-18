# AutoCAD Format Support Matrix

UCAD distinguishes **container transport** from **semantic round-trip fidelity**. A file extension is not advertised as supported until its transport adapter exists, and successful container parsing does not imply that every AutoCAD object type is editable by UCAD.

## Drawing containers

| Format | Open / Import | Export | Current transport | Current fidelity boundary |
| --- | --- | --- | --- | --- |
| `.dwg` | Yes | Yes | ACadSharp DWG ↔ shared DXF bridge | UCAD-supported DXF entities/tables |
| `.dxf` | Yes | Yes | UCAD ASCII DXF + ACadSharp binary/legacy normalization | UCAD-supported DXF entities/tables |
| `.dwt` | Yes | Yes | DWG-compatible template container | Template-specific metadata may be reduced |
| `.dws` | Yes | No | DWG-compatible standards container | Geometry/tables only; standards rules are not authored |
| `.bak` | Yes | No | DWG-compatible recovery source | Recovery import only |
| `.sv$` | Yes | No | DWG-compatible recovery source | Recovery import only |

Imported AutoCAD files are intentionally opened without a native UCAD save path. Normal **Save** therefore asks for a `.ucad` destination instead of silently overwriting a source file that may contain unsupported objects.

## Recognized formats awaiting adapters

| Category | Formats | Status |
| --- | --- | --- |
| Published / plotting | `.dwf`, `.dwfx`, `.ctb`, `.stb`, `.pc3`, `.pmp` | Recognized; no open/export capability is advertised yet |
| Resource / customization | `.dxb`, `.pat`, `.lin`, `.shx`, `.cuix`, `.arg` | Recognized; adapter work is pending |
| Automation | `.scr`, `.lsp`, `.fas`, `.vlx` | Recognized; no AutoCAD/Visual LISP runtime is claimed |

## DWG/DXF version transport

The current ACadSharp bridge targets the AC1032 generation for UCAD exports. Reader/writer version coverage is constrained by the upstream transport library and UCAD's own semantic mapping. Version-specific regression fixtures will be added before v0.9 acceptance.

## v0.9 fidelity priorities

1. Dimensions and annotation: `DIMENSION`, `MTEXT`, leaders and styles.
2. Blocks and attributes: block definitions, `INSERT`, `ATTRIB`, `ATTDEF`.
3. Hatch and linetype fidelity, including pattern metadata.
4. Layouts, viewports, page setup and advanced object tables.
5. Explicit proxy preservation for unsupported/custom DWG objects.
6. Multi-version fixtures and large-drawing round-trip regression.

The capability registry in `CadAcadFileFormatRegistry` is the source of truth used by the application UI. Pending formats must remain non-openable/non-exportable there until a real adapter and regression coverage are present.
