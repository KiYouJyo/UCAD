from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
def read(p): return (ROOT/p).read_text(encoding='utf-8')
def write(p,s): (ROOT/p).write_text(s,encoding='utf-8',newline='\n')

DATA={
'UcadIconCommand':'M3,3 L13,3 L13,5 L3,5 Z M3,7 L13,7 L13,9 L3,9 Z M3,11 L13,11 L13,13 L3,13 Z',
'UcadIconSelect':'M2,1 L13,8 L8,9 L6,14 L2,1 Z',
'UcadIconLine':'M1,13 L2.5,14.5 L15,2 L13.5,0.5 Z',
'UcadIconPolyline':'M1,12 L2,14 L7,9 L6,7 Z M6,7 L7,9 L10,6 L9,4 Z M9,4 L10,6 L15,1 L14,0 Z',
'UcadIconRectangle':'M2,2 L14,2 L14,3.5 L2,3.5 Z M2,12.5 L14,12.5 L14,14 L2,14 Z M2,3.5 L3.5,3.5 L3.5,12.5 L2,12.5 Z M12.5,3.5 L14,3.5 L14,12.5 L12.5,12.5 Z',
'UcadIconCircle':'M8,1 C11.87,1 15,4.13 15,8 C15,11.87 11.87,15 8,15 C4.13,15 1,11.87 1,8 C1,4.13 4.13,1 8,1 Z M8,3 C5.24,3 3,5.24 3,8 C3,10.76 5.24,13 8,13 C10.76,13 13,10.76 13,8 C13,5.24 10.76,3 8,3 Z',
'UcadIconArc':'M2,13 C3,6.5 7,2 14,2 L14,4 C8.2,4 4.4,7.7 4,13.5 Z',
'UcadIconHatch':'M2,2 L14,2 L14,3.3 L2,3.3 Z M2,12.7 L14,12.7 L14,14 L2,14 Z M2,3.3 L3.3,3.3 L3.3,12.7 L2,12.7 Z M12.7,3.3 L14,3.3 L14,12.7 L12.7,12.7 Z M3,10 L4,11 L11,4 L10,3 Z M6,13 L7,14 L14,7 L13,6 Z M2,7 L3,8 L8,3 L7,2 Z',
'UcadIconBoundary':'M1,1 L15,1 L15,2.5 L1,2.5 Z M1,13.5 L15,13.5 L15,15 L1,15 Z M1,2.5 L2.5,2.5 L2.5,13.5 L1,13.5 Z M13.5,2.5 L15,2.5 L15,13.5 L13.5,13.5 Z M5,5 L11,5 L11,6.2 L5,6.2 Z M5,9.8 L11,9.8 L11,11 L5,11 Z M5,6.2 L6.2,6.2 L6.2,9.8 L5,9.8 Z M9.8,6.2 L11,6.2 L11,9.8 L9.8,9.8 Z',
'UcadIconRay':'M1,13 L2.4,14.4 L12,4.8 L10.5,3.4 Z M9,2 L15,1 L14,7 L12.5,4.8 Z',
'UcadIconXLine':'M1,13 L2.4,14.4 L14.5,2.3 L13.1,0.9 Z M0.5,11 L1,15 L5,14 Z M11,1 L15,0.5 L14,5 Z',
'UcadIconMove':'M7,1 L9,1 L9,5 L11,3 L12.4,4.4 L9,7.8 L9,9 L7,9 L7,7.8 L3.6,4.4 L5,3 L7,5 Z M7,9 L9,9 L9,15 L7,15 Z M1,7 L7,7 L7,9 L1,9 Z M9,7 L15,7 L15,9 L9,9 Z',
'UcadIconCopy':'M2,5 L11,5 L11,14 L2,14 Z M4,7 L4,12 L9,12 L9,7 Z M5,2 L14,2 L14,11 L12,11 L12,4 L5,4 Z',
'UcadIconOffset':'M2,4 L14,4 L14,6 L2,6 Z M2,10 L14,10 L14,12 L2,12 Z',
'UcadIconTrim':'M2,3 L3,2 L8,7 L13,2 L14,3 L9,8 L14,13 L13,14 L8,9 L3,14 L2,13 L7,8 Z',
'UcadIconRotate':'M3,10 C3,5.5 6,3 10,3 L10,1 L15,4 L10,7 L10,5 C7.2,5 5,6.8 5,10 C5,12 6,13 8,14 L6,15 C4,14 3,12 3,10 Z',
'UcadIconScale':'M2,2 L7,2 L7,4 L4,4 L4,7 L2,7 Z M9,9 L14,9 L14,14 L9,14 L9,12 L12,12 L12,11 L9,11 Z M6,10 L10,6 L8,6 L8,4 L13,4 L13,9 L11,9 L11,7 L7,11 Z',
'UcadIconMirror':'M7,1 L9,1 L9,15 L7,15 Z M1,4 L6,2 L6,14 L1,12 Z M15,4 L10,2 L10,14 L15,12 Z',
'UcadIconExtend':'M2,5 L10,5 L10,7 L2,7 Z M2,10 L14,10 L14,12 L2,12 Z M10,2 L15,6 L10,9 L10,7 L12,6 L10,5 Z',
'UcadIconExplode':'M6,6 L10,6 L10,10 L6,10 Z M1,1 L5,2 L4,6 L1,5 Z M11,2 L15,1 L15,5 L12,6 Z M1,11 L4,10 L5,14 L1,15 Z M12,10 L15,11 L15,15 L11,14 Z',
'UcadIconText':'M2,2 L14,2 L14,5 L12,5 L12,4 L9,4 L9,13 L11,13 L11,15 L5,15 L5,13 L7,13 L7,4 L4,4 L4,5 L2,5 Z',
'UcadIconDimension':'M2,3 L3.5,3 L3.5,13 L2,13 Z M12.5,3 L14,3 L14,13 L12.5,13 Z M4,8 L6.5,6 L6.5,7.2 L9.5,7.2 L9.5,6 L12,8 L9.5,10 L9.5,8.8 L6.5,8.8 L6.5,10 Z',
'UcadIconLayer':'M2,5 L8,2 L14,5 L8,8 Z M2,8 L4,7 L8,9 L12,7 L14,8 L8,11 Z M2,11 L4,10 L8,12 L12,10 L14,11 L8,14 Z',
'UcadIconProperties':'M2,3 L8,3 L8,5 L2,5 Z M11,3 L14,3 L14,5 L11,5 Z M8,2 L11,2 L11,6 L8,6 Z M2,8 L4,8 L4,10 L2,10 Z M7,8 L14,8 L14,10 L7,10 Z M4,7 L7,7 L7,11 L4,11 Z',
'UcadIconBlock':'M2,2 L7,2 L7,7 L2,7 Z M9,2 L14,2 L14,7 L9,7 Z M2,9 L7,9 L7,14 L2,14 Z M9,9 L14,9 L14,14 L9,14 Z',
'UcadIconInsert':'M2,2 L10,2 L10,10 L2,10 Z M4,4 L8,4 L8,8 L4,8 Z M11,6 L13,6 L13,10 L15,10 L12,14 L9,10 L11,10 Z',
'UcadIconEllipse':'M8,2 C12.4,2 15,4.7 15,8 C15,11.3 12.4,14 8,14 C3.6,14 1,11.3 1,8 C1,4.7 3.6,2 8,2 Z M8,4 C4.8,4 3,5.8 3,8 C3,10.2 4.8,12 8,12 C11.2,12 13,10.2 13,8 C13,5.8 11.2,4 8,4 Z',
'UcadIconPolygon':'M8,1 L14,4.5 L14,11.5 L8,15 L2,11.5 L2,4.5 Z M8,3.2 L4,5.5 L4,10.5 L8,12.8 L12,10.5 L12,5.5 Z',
'UcadIconSpline':'M1,12 C4,2 7,14 10,5 C12,1 14,2 15,3 L14,5 C13,3.5 12,3.5 11,6 C8,15 4,5 2,13 Z',
'UcadIconPoint':'M7,1 L9,1 L9,6 L14,6 L14,8 L9,8 L9,15 L7,15 L7,8 L2,8 L2,6 L7,6 Z',
'UcadIconLeader':'M2,13 L3.2,14.2 L8,9.4 L14,9.4 L14,7.8 L7.4,7.8 Z M1,15 L1.7,11.5 L4.5,14.3 Z',
'UcadIconFillet':'M2,2 L4,2 L4,8 C4,10.2 5.8,12 8,12 L14,12 L14,14 L8,14 C4.7,14 2,11.3 2,8 Z',
'UcadIconChamfer':'M2,2 L4,2 L4,9 L7,12 L14,12 L14,14 L6,14 L2,10 Z',
'UcadIconArray':'M2,2 L5,2 L5,5 L2,5 Z M6.5,2 L9.5,2 L9.5,5 L6.5,5 Z M11,2 L14,2 L14,5 L11,5 Z M2,6.5 L5,6.5 L5,9.5 L2,9.5 Z M6.5,6.5 L9.5,6.5 L9.5,9.5 L6.5,9.5 Z M11,6.5 L14,6.5 L14,9.5 L11,9.5 Z M2,11 L5,11 L5,14 L2,14 Z M6.5,11 L9.5,11 L9.5,14 L6.5,14 Z M11,11 L14,11 L14,14 L11,14 Z',
'UcadIconBreak':'M1,7 L6,7 L6,9 L1,9 Z M10,7 L15,7 L15,9 L10,9 Z M6,4 L8,7 L6.8,7.8 L5,5 Z M10,9 L8,12 L6.8,11.2 L8.8,8.5 Z',
'UcadIconJoin':'M1,12 L2.4,13.4 L8,7.8 L13.6,13.4 L15,12 L8,5 Z M7,4 L9,4 L9,7 L7,7 Z',
'UcadIconStretch':'M2,4 L10,4 L10,6 L2,6 Z M2,10 L10,10 L10,12 L2,12 Z M11,6 L15,8 L11,10 Z',
}

# Static PathIcon markup gets a unique, inline Geometry instance through XAML's converter.
xaml=read('src/UCAD.App/MainWindow.xaml')
for key,data in DATA.items():
    xaml=xaml.replace(f'Data="{{StaticResource {key}}}"',f'Data="{data}"')
write('src/UCAD.App/MainWindow.xaml',xaml)

# ResourceDictionary is no longer merged: WinUI cannot safely reuse Geometry dependency objects as PathIcon.Data.
app=read('src/UCAD.App/App.xaml')
app=app.replace('                <ResourceDictionary Source="ms-appx:///Styles/UcadCadIcons.xaml" />\n','')
write('src/UCAD.App/App.xaml',app)

# Dynamic command buttons use the same centralized path-data registry, parsed into fresh PathGeometry instances.
cmdmap={
'LINE':'UcadIconLine','PLINE':'UcadIconPolyline','RECTANGLE':'UcadIconRectangle','CIRCLE':'UcadIconCircle','ARC':'UcadIconArc',
'HATCH':'UcadIconHatch','HATCHADV':'UcadIconHatch','HATCHEDIT':'UcadIconHatch','BOUNDARY':'UcadIconBoundary','RAY':'UcadIconRay','XLINE':'UcadIconXLine',
'MOVE':'UcadIconMove','COPY':'UcadIconCopy','OFFSET':'UcadIconOffset','TRIM':'UcadIconTrim','ROTATE':'UcadIconRotate','SCALE':'UcadIconScale','MIRROR':'UcadIconMirror','EXTEND':'UcadIconExtend','EXPLODE':'UcadIconExplode',
'TEXT':'UcadIconText','MTEXT':'UcadIconText','DIM':'UcadIconDimension','DIMALIGNED':'UcadIconDimension','DIMANGULAR':'UcadIconDimension','DIMRADIUS':'UcadIconDimension','DIMDIAMETER':'UcadIconDimension','LEADER':'UcadIconLeader',
'LAYER':'UcadIconLayer','CHPROP':'UcadIconProperties','TEXTSTYLE':'UcadIconProperties','DIMSTYLE':'UcadIconProperties','BLOCK':'UcadIconBlock','BLOCKMANAGER':'UcadIconBlock','BLOCKREDEFINE':'UcadIconBlock','INSERT':'UcadIconInsert','XREF':'UcadIconInsert','ATTDEF':'UcadIconText','ATTEDIT':'UcadIconProperties',
'ELLIPSE':'UcadIconEllipse','POLYGON':'UcadIconPolygon','SPLINE':'UcadIconSpline','POINT':'UcadIconPoint','FILLET':'UcadIconFillet','CHAMFER':'UcadIconChamfer','ARRAY':'UcadIconArray','BREAK':'UcadIconBreak','JOIN':'UcadIconJoin','STRETCH':'UcadIconStretch','PEDIT':'UcadIconPolyline'}

def cs(s): return s.replace('\\','\\\\').replace('"','\\"')
reg='\n'.join(f'        ["{k}"] = "{cs(v)}",' for k,v in DATA.items())
mapcs='\n'.join(f'        ["{k}"] = "{v}",' for k,v in cmdmap.items())
service=f'''using System.Globalization;\nusing System.Text.RegularExpressions;\nusing Microsoft.UI.Xaml;\nusing Microsoft.UI.Xaml.Controls;\nusing Microsoft.UI.Xaml.Media;\nusing Windows.Foundation;\n\nnamespace UCAD.Services;\n\ninternal static class CadToolIconService\n{{\n    private static readonly IReadOnlyDictionary<string,string> PathData = new Dictionary<string,string>(StringComparer.Ordinal)\n    {{\n{reg}\n    }};\n\n    private static readonly IReadOnlyDictionary<string,string> CommandKeys = new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase)\n    {{\n{mapcs}\n    }};\n\n    private static readonly Regex Tokens = new(@"[MLCZ]|-?\\d+(?:\\.\\d+)?", RegexOptions.Compiled | RegexOptions.CultureInvariant);\n\n    internal static PathIcon Create(string command, double size = 16)\n    {{\n        var normalized = (command ?? string.Empty).Split('|').LastOrDefault() ?? string.Empty;\n        var key = CommandKeys.TryGetValue(normalized, out var found) ? found : "UcadIconCommand";\n        return new PathIcon {{ Data = Parse(PathData[key], key is "UcadIconCircle" or "UcadIconEllipse" or "UcadIconPolygon"), Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Center }};\n    }}\n\n    private static PathGeometry Parse(string data, bool evenOdd)\n    {{\n        var tokens = Tokens.Matches(data).Select(m => m.Value).ToArray();\n        var geometry = new PathGeometry {{ FillRule = evenOdd ? FillRule.EvenOdd : FillRule.Nonzero }};\n        PathFigure? figure = null;\n        var i = 0;\n        double Number() => double.Parse(tokens[i++], CultureInfo.InvariantCulture);\n        Point Point() => new(Number(), Number());\n        while (i < tokens.Length)\n        {{\n            var command = tokens[i++];\n            switch (command)\n            {{\n                case "M":\n                    figure = new PathFigure {{ StartPoint = Point() }};\n                    geometry.Figures.Add(figure);\n                    break;\n                case "L":\n                    (figure ?? throw new FormatException("LINE without figure")).Segments.Add(new LineSegment {{ Point = Point() }});\n                    break;\n                case "C":\n                    (figure ?? throw new FormatException("BEZIER without figure")).Segments.Add(new BezierSegment {{ Point1 = Point(), Point2 = Point(), Point3 = Point() }});\n                    break;\n                case "Z":\n                    (figure ?? throw new FormatException("CLOSE without figure")).IsClosed = true;\n                    break;\n                default:\n                    throw new FormatException($"Unsupported CAD icon path command: {{command}}");\n            }}\n        }}\n        return geometry;\n    }}\n}}\n'''
write('src/UCAD.App/Services/CadToolIconService.cs',service)

val=read('.github/scripts/Validate-UcadUi.ps1')
old='''# CAD icon system: professional tool surfaces must use centralized filled Geometry resources.\n$cadIcons = Get-Content src/UCAD.App/Styles/UcadCadIcons.xaml -Raw\nAssert-Contains $cadIcons @('UcadIconLine','UcadIconPolyline','UcadIconCircle','UcadIconArc','UcadIconHatch','UcadIconMove','UcadIconTrim','UcadIconEllipse','UcadIconSpline','UcadIconFillet','UcadIconArray') 'CAD icon resources'\n$iconService = Get-Content src/UCAD.App/Services/CadToolIconService.cs -Raw\nAssert-Contains $iconService @('CadToolIconService','UcadIconLine','UcadIconDimension','UcadIconBlock','UcadIconStretch') 'CAD icon service'\nif ($xaml -match 'Data="M2,14 L14,2"|Data="M2,13 L6,7 L10,11 L14,3"|Glyph="&#xE7C2;"|Glyph="&#xE8C8;"|Glyph="&#xE78A;"') { throw 'Legacy/broken CAD toolbar icon markup remains.' }\n'''
new='''# CAD icon system: static tools use closed inline vectors; dynamic tools use the centralized data registry and fresh Geometry instances.\n$iconService = Get-Content src/UCAD.App/Services/CadToolIconService.cs -Raw\nAssert-Contains $iconService @('CadToolIconService','PathData','CommandKeys','UcadIconLine','UcadIconDimension','UcadIconBlock','UcadIconStretch','new PathGeometry','new PathFigure','new LineSegment','new BezierSegment') 'CAD icon registry'\nAssert-Contains $xaml @('Data="M1,13 L2.5,14.5 L15,2 L13.5,0.5 Z"','Data="M2,3 L3,2 L8,7 L13,2 L14,3 L9,8 L14,13 L13,14 L8,9 L3,14 L2,13 L7,8 Z"') 'Static CAD vector icons'\nif ($xaml -match '\\{StaticResource UcadIcon|Data="M2,14 L14,2"|Data="M2,13 L6,7 L10,11 L14,3"|Glyph="&#xE7C2;"|Glyph="&#xE8C8;"|Glyph="&#xE78A;"') { throw 'Legacy/shared CAD toolbar icon markup remains.' }\n'''
if old not in val: raise RuntimeError('validator icon block not found')
write('.github/scripts/Validate-UcadUi.ps1',val.replace(old,new))
print('v0.8.1 icon registry convergence applied')
