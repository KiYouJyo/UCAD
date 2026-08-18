from pathlib import Path
import json

ROOT=Path(__file__).resolve().parents[2]

def read(p): return (ROOT/p).read_text(encoding='utf-8')
def write(p,s):
    q=ROOT/p; q.parent.mkdir(parents=True,exist_ok=True); q.write_text(s,encoding='utf-8',newline='\n')
def repl(p,a,b,count=None):
    s=read(p); n=s.count(a)
    if n==0: return
    if count is not None and n!=count: raise RuntimeError(f'{p}: expected {count} matches, got {n}: {a[:80]}')
    write(p,s.replace(a,b))

icons='''<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
  <Geometry x:Key="UcadIconCommand">M3,3 L13,3 L13,5 L3,5 Z M3,7 L13,7 L13,9 L3,9 Z M3,11 L13,11 L13,13 L3,13 Z</Geometry>
  <Geometry x:Key="UcadIconSelect">M2,1 L13,8 L8,9 L6,14 L2,1 Z</Geometry>
  <Geometry x:Key="UcadIconLine">M1,13 L2.5,14.5 L15,2 L13.5,.5 Z</Geometry>
  <Geometry x:Key="UcadIconPolyline">M1,12 L2,14 L7,9 L6,7 Z M6,7 L7,9 L10,6 L9,4 Z M9,4 L10,6 L15,1 L14,0 Z</Geometry>
  <Geometry x:Key="UcadIconRectangle">M2,2 L14,2 L14,3.5 L2,3.5 Z M2,12.5 L14,12.5 L14,14 L2,14 Z M2,3.5 L3.5,3.5 L3.5,12.5 L2,12.5 Z M12.5,3.5 L14,3.5 L14,12.5 L12.5,12.5 Z</Geometry>
  <Geometry x:Key="UcadIconCircle">F0 M8,1 C11.87,1 15,4.13 15,8 C15,11.87 11.87,15 8,15 C4.13,15 1,11.87 1,8 C1,4.13 4.13,1 8,1 Z M8,3 C5.24,3 3,5.24 3,8 C3,10.76 5.24,13 8,13 C10.76,13 13,10.76 13,8 C13,5.24 10.76,3 8,3 Z</Geometry>
  <Geometry x:Key="UcadIconArc">M2,13 C3,6.5 7,2 14,2 L14,4 C8.2,4 4.4,7.7 4,13.5 Z</Geometry>
  <Geometry x:Key="UcadIconHatch">M2,2 L14,2 L14,3.3 L2,3.3 Z M2,12.7 L14,12.7 L14,14 L2,14 Z M2,3.3 L3.3,3.3 L3.3,12.7 L2,12.7 Z M12.7,3.3 L14,3.3 L14,12.7 L12.7,12.7 Z M3,10 L4,11 L11,4 L10,3 Z M6,13 L7,14 L14,7 L13,6 Z M2,7 L3,8 L8,3 L7,2 Z</Geometry>
  <Geometry x:Key="UcadIconBoundary">M1,1 L15,1 L15,2.5 L1,2.5 Z M1,13.5 L15,13.5 L15,15 L1,15 Z M1,2.5 L2.5,2.5 L2.5,13.5 L1,13.5 Z M13.5,2.5 L15,2.5 L15,13.5 L13.5,13.5 Z M5,5 L11,5 L11,6.2 L5,6.2 Z M5,9.8 L11,9.8 L11,11 L5,11 Z M5,6.2 L6.2,6.2 L6.2,9.8 L5,9.8 Z M9.8,6.2 L11,6.2 L11,9.8 L9.8,9.8 Z</Geometry>
  <Geometry x:Key="UcadIconRay">M1,13 L2.4,14.4 L12,4.8 L10.5,3.4 Z M9,2 L15,1 L14,7 L12.5,4.8 Z</Geometry>
  <Geometry x:Key="UcadIconXLine">M1,13 L2.4,14.4 L14.5,2.3 L13.1,.9 Z M.5,11 L1,15 L5,14 Z M11,1 L15,.5 L14,5 Z</Geometry>
  <Geometry x:Key="UcadIconMove">M7,1 L9,1 L9,5 L11,3 L12.4,4.4 L9,7.8 L9,9 L7,9 L7,7.8 L3.6,4.4 L5,3 L7,5 Z M7,9 L9,9 L9,15 L7,15 Z M1,7 L7,7 L7,9 L1,9 Z M9,7 L15,7 L15,9 L9,9 Z</Geometry>
  <Geometry x:Key="UcadIconCopy">M2,5 L11,5 L11,14 L2,14 Z M4,7 L4,12 L9,12 L9,7 Z M5,2 L14,2 L14,11 L12,11 L12,4 L5,4 Z</Geometry>
  <Geometry x:Key="UcadIconOffset">M2,4 L14,4 L14,6 L2,6 Z M2,10 L14,10 L14,12 L2,12 Z</Geometry>
  <Geometry x:Key="UcadIconTrim">M2,3 L3,2 L8,7 L13,2 L14,3 L9,8 L14,13 L13,14 L8,9 L3,14 L2,13 L7,8 Z</Geometry>
  <Geometry x:Key="UcadIconRotate">M3,10 C3,5.5 6,3 10,3 L10,1 L15,4 L10,7 L10,5 C7.2,5 5,6.8 5,10 C5,12 6,13 8,14 L6,15 C4,14 3,12 3,10 Z</Geometry>
  <Geometry x:Key="UcadIconScale">M2,2 L7,2 L7,4 L4,4 L4,7 L2,7 Z M9,9 L14,9 L14,14 L9,14 L9,12 L12,12 L12,11 L9,11 Z M6,10 L10,6 L8,6 L8,4 L13,4 L13,9 L11,9 L11,7 L7,11 Z</Geometry>
  <Geometry x:Key="UcadIconMirror">M7,1 L9,1 L9,15 L7,15 Z M1,4 L6,2 L6,14 L1,12 Z M15,4 L10,2 L10,14 L15,12 Z</Geometry>
  <Geometry x:Key="UcadIconExtend">M2,5 L10,5 L10,7 L2,7 Z M2,10 L14,10 L14,12 L2,12 Z M10,2 L15,6 L10,9 L10,7 L12,6 L10,5 Z</Geometry>
  <Geometry x:Key="UcadIconExplode">M6,6 L10,6 L10,10 L6,10 Z M1,1 L5,2 L4,6 L1,5 Z M11,2 L15,1 L15,5 L12,6 Z M1,11 L4,10 L5,14 L1,15 Z M12,10 L15,11 L15,15 L11,14 Z</Geometry>
  <Geometry x:Key="UcadIconText">M2,2 L14,2 L14,5 L12,5 L12,4 L9,4 L9,13 L11,13 L11,15 L5,15 L5,13 L7,13 L7,4 L4,4 L4,5 L2,5 Z</Geometry>
  <Geometry x:Key="UcadIconDimension">M2,3 L3.5,3 L3.5,13 L2,13 Z M12.5,3 L14,3 L14,13 L12.5,13 Z M4,8 L6.5,6 L6.5,7.2 L9.5,7.2 L9.5,6 L12,8 L9.5,10 L9.5,8.8 L6.5,8.8 L6.5,10 Z</Geometry>
  <Geometry x:Key="UcadIconLayer">M2,5 L8,2 L14,5 L8,8 Z M2,8 L4,7 L8,9 L12,7 L14,8 L8,11 Z M2,11 L4,10 L8,12 L12,10 L14,11 L8,14 Z</Geometry>
  <Geometry x:Key="UcadIconProperties">M2,3 L8,3 L8,5 L2,5 Z M11,3 L14,3 L14,5 L11,5 Z M8,2 L11,2 L11,6 L8,6 Z M2,8 L4,8 L4,10 L2,10 Z M7,8 L14,8 L14,10 L7,10 Z M4,7 L7,7 L7,11 L4,11 Z</Geometry>
  <Geometry x:Key="UcadIconBlock">M2,2 L7,2 L7,7 L2,7 Z M9,2 L14,2 L14,7 L9,7 Z M2,9 L7,9 L7,14 L2,14 Z M9,9 L14,9 L14,14 L9,14 Z</Geometry>
  <Geometry x:Key="UcadIconInsert">M2,2 L10,2 L10,10 L2,10 Z M4,4 L8,4 L8,8 L4,8 Z M11,6 L13,6 L13,10 L15,10 L12,14 L9,10 L11,10 Z</Geometry>
  <Geometry x:Key="UcadIconEllipse">F0 M8,2 C12.4,2 15,4.7 15,8 C15,11.3 12.4,14 8,14 C3.6,14 1,11.3 1,8 C1,4.7 3.6,2 8,2 Z M8,4 C4.8,4 3,5.8 3,8 C3,10.2 4.8,12 8,12 C11.2,12 13,10.2 13,8 C13,5.8 11.2,4 8,4 Z</Geometry>
  <Geometry x:Key="UcadIconPolygon">F0 M8,1 L14,4.5 L14,11.5 L8,15 L2,11.5 L2,4.5 Z M8,3.2 L4,5.5 L4,10.5 L8,12.8 L12,10.5 L12,5.5 Z</Geometry>
  <Geometry x:Key="UcadIconSpline">M1,12 C4,2 7,14 10,5 C12,1 14,2 15,3 L14,5 C13,3.5 12,3.5 11,6 C8,15 4,5 2,13 Z</Geometry>
  <Geometry x:Key="UcadIconPoint">M7,1 L9,1 L9,6 L14,6 L14,8 L9,8 L9,15 L7,15 L7,8 L2,8 L2,6 L7,6 Z</Geometry>
  <Geometry x:Key="UcadIconLeader">M2,13 L3.2,14.2 L8,9.4 L14,9.4 L14,7.8 L7.4,7.8 Z M1,15 L1.7,11.5 L4.5,14.3 Z</Geometry>
  <Geometry x:Key="UcadIconFillet">M2,2 L4,2 L4,8 C4,10.2 5.8,12 8,12 L14,12 L14,14 L8,14 C4.7,14 2,11.3 2,8 Z</Geometry>
  <Geometry x:Key="UcadIconChamfer">M2,2 L4,2 L4,9 L7,12 L14,12 L14,14 L6,14 L2,10 Z</Geometry>
  <Geometry x:Key="UcadIconArray">M2,2 L5,2 L5,5 L2,5 Z M6.5,2 L9.5,2 L9.5,5 L6.5,5 Z M11,2 L14,2 L14,5 L11,5 Z M2,6.5 L5,6.5 L5,9.5 L2,9.5 Z M6.5,6.5 L9.5,6.5 L9.5,9.5 L6.5,9.5 Z M11,6.5 L14,6.5 L14,9.5 L11,9.5 Z M2,11 L5,11 L5,14 L2,14 Z M6.5,11 L9.5,11 L9.5,14 L6.5,14 Z M11,11 L14,11 L14,14 L11,14 Z</Geometry>
  <Geometry x:Key="UcadIconBreak">M1,7 L6,7 L6,9 L1,9 Z M10,7 L15,7 L15,9 L10,9 Z M6,4 L8,7 L6.8,7.8 L5,5 Z M10,9 L8,12 L6.8,11.2 L8.8,8.5 Z</Geometry>
  <Geometry x:Key="UcadIconJoin">M1,12 L2.4,13.4 L8,7.8 L13.6,13.4 L15,12 L8,5 Z M7,4 L9,4 L9,7 L7,7 Z</Geometry>
  <Geometry x:Key="UcadIconStretch">M2,4 L10,4 L10,6 L2,6 Z M2,10 L10,10 L10,12 L2,12 Z M11,6 L15,8 L11,10 Z</Geometry>
</ResourceDictionary>'''
write('src/UCAD.App/Styles/UcadCadIcons.xaml',icons+'\n')

svc='''using Microsoft.UI.Xaml;\nusing Microsoft.UI.Xaml.Controls;\nusing Microsoft.UI.Xaml.Media;\n\nnamespace UCAD.Services;\n\ninternal static class CadToolIconService\n{\n    private static readonly Dictionary<string,string> Keys = new(StringComparer.OrdinalIgnoreCase)\n    {\n        ["LINE"]="UcadIconLine", ["PLINE"]="UcadIconPolyline", ["RECTANGLE"]="UcadIconRectangle", ["CIRCLE"]="UcadIconCircle", ["ARC"]="UcadIconArc",\n        ["HATCH"]="UcadIconHatch", ["HATCHADV"]="UcadIconHatch", ["HATCHEDIT"]="UcadIconHatch", ["BOUNDARY"]="UcadIconBoundary", ["RAY"]="UcadIconRay", ["XLINE"]="UcadIconXLine",\n        ["MOVE"]="UcadIconMove", ["COPY"]="UcadIconCopy", ["OFFSET"]="UcadIconOffset", ["TRIM"]="UcadIconTrim", ["ROTATE"]="UcadIconRotate", ["SCALE"]="UcadIconScale", ["MIRROR"]="UcadIconMirror", ["EXTEND"]="UcadIconExtend",\n        ["EXPLODE"]="UcadIconExplode", ["TEXT"]="UcadIconText", ["MTEXT"]="UcadIconText", ["DIM"]="UcadIconDimension", ["DIMALIGNED"]="UcadIconDimension", ["DIMANGULAR"]="UcadIconDimension", ["DIMRADIUS"]="UcadIconDimension", ["DIMDIAMETER"]="UcadIconDimension", ["LEADER"]="UcadIconLeader",\n        ["LAYER"]="UcadIconLayer", ["CHPROP"]="UcadIconProperties", ["TEXTSTYLE"]="UcadIconProperties", ["DIMSTYLE"]="UcadIconProperties", ["BLOCK"]="UcadIconBlock", ["BLOCKMANAGER"]="UcadIconBlock", ["BLOCKREDEFINE"]="UcadIconBlock", ["INSERT"]="UcadIconInsert", ["XREF"]="UcadIconInsert", ["ATTDEF"]="UcadIconText", ["ATTEDIT"]="UcadIconProperties",\n        ["ELLIPSE"]="UcadIconEllipse", ["POLYGON"]="UcadIconPolygon", ["SPLINE"]="UcadIconSpline", ["POINT"]="UcadIconPoint", ["FILLET"]="UcadIconFillet", ["CHAMFER"]="UcadIconChamfer", ["ARRAY"]="UcadIconArray", ["BREAK"]="UcadIconBreak", ["JOIN"]="UcadIconJoin", ["STRETCH"]="UcadIconStretch", ["PEDIT"]="UcadIconPolyline"\n    };\n\n    internal static PathIcon Create(string command, double size=16)\n    {\n        var normalized=(command ?? string.Empty).Split('|').LastOrDefault() ?? string.Empty;\n        var key=Keys.TryGetValue(normalized,out var found)?found:"UcadIconCommand";\n        if (Application.Current.Resources[key] is not Geometry data) data=(Geometry)Application.Current.Resources["UcadIconCommand"];\n        return new PathIcon { Data=data, Width=size, Height=size, HorizontalAlignment=HorizontalAlignment.Center };\n    }\n}\n'''
write('src/UCAD.App/Services/CadToolIconService.cs',svc)

repl('src/UCAD.App/App.xaml','<ResourceDictionary Source="ms-appx:///Styles/UcadDesignTokens.xaml" />','<ResourceDictionary Source="ms-appx:///Styles/UcadDesignTokens.xaml" />\n                <ResourceDictionary Source="ms-appx:///Styles/UcadCadIcons.xaml" />')

x='src/UCAD.App/MainWindow.xaml'
for a,b in {
'Data="M2,14 L14,2"':'Data="{StaticResource UcadIconLine}"',
'Data="M2,13 L6,7 L10,11 L14,3"':'Data="{StaticResource UcadIconPolyline}"',
'Data="M2,3 L14,3 L14,13 L2,13 Z"':'Data="{StaticResource UcadIconRectangle}"',
'Data="M8,2 A6,6 0 1 1 7.99,2"':'Data="{StaticResource UcadIconCircle}"',
'Data="M2,13 A11,11 0 0 1 13,2"':'Data="{StaticResource UcadIconArc}"',
'Data="M2,13 L13,2 M6,14 L14,6 M2,9 L9,2"':'Data="{StaticResource UcadIconHatch}"',
'Data="M2,2 L14,2 L14,14 L2,14 Z M5,5 L11,5 L11,11 L5,11 Z"':'Data="{StaticResource UcadIconBoundary}"',
'Data="M2,14 L13,3 M8,3 L13,3 L13,8"':'Data="{StaticResource UcadIconRay}"',
'Data="M2,8 L14,8 M8,2 L8,14"':'Data="{StaticResource UcadIconXLine}"',
'Data="M2,5 L14,5 M2,11 L14,11"':'Data="{StaticResource UcadIconOffset}"',
'Data="M5 3.05854C5 2.21347 5.98325 1.74939 6.63564 2.28655L17.6418 11.3487C18.3661 11.9451 17.9444 13.1207 17.0061 13.1207H11.4142C10.9788 13.1207 10.5648 13.3099 10.2799 13.6392L6.75622 17.7117C6.15025 18.412 5 17.9835 5 17.0574L5 3.05854Z"':'Data="{StaticResource UcadIconSelect}"',
'<FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE7C2;" FontSize="16" />':'<PathIcon Width="16" Height="16" Data="{StaticResource UcadIconMove}" />',
'<FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE8C8;" FontSize="16" />':'<PathIcon Width="16" Height="16" Data="{StaticResource UcadIconCopy}" />',
'<FontIcon FontFamily="Segoe Fluent Icons" Glyph="&#xE78A;" FontSize="16" />':'<PathIcon Width="16" Height="16" Data="{StaticResource UcadIconTrim}" />'
}.items(): repl(x,a,b)

for p in ['src/UCAD.App/MainWindow.Authoring.cs','src/UCAD.App/MainWindow.ExtendedDrawing.cs','src/UCAD.App/MainWindow.ModifyShell.cs','src/UCAD.App/MainWindow.ModifyCompletion.cs']:
    s=read(p)
    if 'using UCAD.Services;' not in s:
        idx=s.find('namespace UCAD;'); s=s[:idx]+'using UCAD.Services;\n\n'+s[idx:]
    write(p,s)

repl('src/UCAD.App/MainWindow.Authoring.cs','var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 1 };\n        panel.Children.Add(new TextBlock','var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Spacing = 1 };\n        panel.Children.Add(CadToolIconService.Create(command));\n        panel.Children.Add(new TextBlock')
repl('src/UCAD.App/MainWindow.ExtendedDrawing.cs','Children =\n                {\n                    new TextBlock { Text = command','Children =\n                {\n                    CadToolIconService.Create(command),\n                    new TextBlock { Text = command')
repl('src/UCAD.App/MainWindow.ModifyShell.cs','Children =\n                {\n                    new TextBlock\n                    {\n                        Text = command','Children =\n                {\n                    CadToolIconService.Create(command),\n                    new TextBlock\n                    {\n                        Text = command')
repl('src/UCAD.App/MainWindow.ModifyCompletion.cs','Children =\n                {\n                    new TextBlock { Text = command','Children =\n                {\n                    CadToolIconService.Create(command),\n                    new TextBlock { Text = command')

write('VERSION','0.8.1\n')
r=json.loads(read('release/release.json')); r['product']['version']='0.8.1'; r['product']['packageVersion']='0.8.1.0'; r['product']['releaseTitle']='CAD Icon System & UI Consistency'; write('release/release.json',json.dumps(r,ensure_ascii=False,indent=2)+'\n')
repl('src/UCAD.App/Package.appxmanifest','Version="0.8.0.0"','Version="0.8.1.0"')
repl('.github/scripts/Validate-UcadUi.ps1',"if ($version -ne '0.8.0') { throw \"Expected VERSION 0.8.0, got $version\" }","if ($version -ne '0.8.1') { throw \"Expected VERSION 0.8.1, got $version\" }")
val=read('.github/scripts/Validate-UcadUi.ps1')
marker='# Version SSOT.'
check='''# CAD icon system: professional tool surfaces must use centralized filled Geometry resources.\n$cadIcons = Get-Content src/UCAD.App/Styles/UcadCadIcons.xaml -Raw\nAssert-Contains $cadIcons @('UcadIconLine','UcadIconPolyline','UcadIconCircle','UcadIconArc','UcadIconHatch','UcadIconMove','UcadIconTrim','UcadIconEllipse','UcadIconSpline','UcadIconFillet','UcadIconArray') 'CAD icon resources'\n$iconService = Get-Content src/UCAD.App/Services/CadToolIconService.cs -Raw\nAssert-Contains $iconService @('CadToolIconService','UcadIconLine','UcadIconDimension','UcadIconBlock','UcadIconStretch') 'CAD icon service'\nif ($xaml -match 'Data="M2,14 L14,2"|Data="M2,13 L6,7 L10,11 L14,3"|Glyph="&#xE7C2;"|Glyph="&#xE8C8;"|Glyph="&#xE78A;"') { throw 'Legacy/broken CAD toolbar icon markup remains.' }\n\n'''
if check not in val: write('.github/scripts/Validate-UcadUi.ps1',val.replace(marker,check+marker))

notes='''# UCAD v0.8.1 — CAD Icon System & UI Consistency\n\n- Replaces broken open-path `PathIcon` drawing glyphs with centralized filled vector Geometry resources.\n- Unifies Draw, Modify, Annotate, Layer, Block and extended authoring icons across ToolShelf, ToolRail and dynamically-created command buttons.\n- Adds dedicated icons for extended entities and commands including Ellipse, Polygon, Spline, Point, Stretch, Fillet, Chamfer, Array, Break and Join.\n- Removes CAD-tool dependency on ambiguous Segoe Fluent glyph codepoints while keeping Fluent icons for genuine shell/system actions.\n- Adds icon-system regression contracts and preserves the complete v0.8 document/DXF/plot/GIS feature set.\n'''
write('release/notes/v0.8.1-en-US.md',notes)
write('release/notes/v0.8.1-zh-CN.md',notes.replace('# UCAD v0.8.1 — CAD Icon System & UI Consistency','# UCAD v0.8.1 — CAD 图标系统与界面一致性'))
write('release/notes/v0.8.1-ja-JP.md',notes.replace('# UCAD v0.8.1 — CAD Icon System & UI Consistency','# UCAD v0.8.1 — CAD アイコンシステムと UI 一貫性'))
print('UCAD v0.8.1 icon pass applied')
