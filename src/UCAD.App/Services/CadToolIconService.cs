using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace UCAD.Services;

internal static class CadToolIconService
{
    private static readonly Dictionary<string,string> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["LINE"]="UcadIconLine", ["PLINE"]="UcadIconPolyline", ["RECTANGLE"]="UcadIconRectangle", ["CIRCLE"]="UcadIconCircle", ["ARC"]="UcadIconArc",
        ["HATCH"]="UcadIconHatch", ["HATCHADV"]="UcadIconHatch", ["HATCHEDIT"]="UcadIconHatch", ["BOUNDARY"]="UcadIconBoundary", ["RAY"]="UcadIconRay", ["XLINE"]="UcadIconXLine",
        ["MOVE"]="UcadIconMove", ["COPY"]="UcadIconCopy", ["OFFSET"]="UcadIconOffset", ["TRIM"]="UcadIconTrim", ["ROTATE"]="UcadIconRotate", ["SCALE"]="UcadIconScale", ["MIRROR"]="UcadIconMirror", ["EXTEND"]="UcadIconExtend",
        ["EXPLODE"]="UcadIconExplode", ["TEXT"]="UcadIconText", ["MTEXT"]="UcadIconText", ["DIM"]="UcadIconDimension", ["DIMALIGNED"]="UcadIconDimension", ["DIMANGULAR"]="UcadIconDimension", ["DIMRADIUS"]="UcadIconDimension", ["DIMDIAMETER"]="UcadIconDimension", ["LEADER"]="UcadIconLeader",
        ["LAYER"]="UcadIconLayer", ["CHPROP"]="UcadIconProperties", ["TEXTSTYLE"]="UcadIconProperties", ["DIMSTYLE"]="UcadIconProperties", ["BLOCK"]="UcadIconBlock", ["BLOCKMANAGER"]="UcadIconBlock", ["BLOCKREDEFINE"]="UcadIconBlock", ["INSERT"]="UcadIconInsert", ["XREF"]="UcadIconInsert", ["ATTDEF"]="UcadIconText", ["ATTEDIT"]="UcadIconProperties",
        ["ELLIPSE"]="UcadIconEllipse", ["POLYGON"]="UcadIconPolygon", ["SPLINE"]="UcadIconSpline", ["POINT"]="UcadIconPoint", ["FILLET"]="UcadIconFillet", ["CHAMFER"]="UcadIconChamfer", ["ARRAY"]="UcadIconArray", ["BREAK"]="UcadIconBreak", ["JOIN"]="UcadIconJoin", ["STRETCH"]="UcadIconStretch", ["PEDIT"]="UcadIconPolyline"
    };

    internal static PathIcon Create(string command, double size=16)
    {
        var normalized=(command ?? string.Empty).Split('|').LastOrDefault() ?? string.Empty;
        var key=Keys.TryGetValue(normalized,out var found)?found:"UcadIconCommand";
        if (Application.Current.Resources[key] is not Geometry data) data=(Geometry)Application.Current.Resources["UcadIconCommand"];
        return new PathIcon { Data=data, Width=size, Height=size, HorizontalAlignment=HorizontalAlignment.Center };
    }
}
