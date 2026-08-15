using System.Globalization;
using UCAD.Core.Geometry;

namespace UCAD.Core.Commands;

public static class CommandInputParser
{
    public static bool TryParseNumber(string? input, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        return double.TryParse(
            input.Trim(),
            NumberStyles.Float | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture,
            out value) && double.IsFinite(value);
    }

    public static bool TryParsePoint(string? input, CadPoint? basePoint, out CadPoint point)
    {
        point = default;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.Trim();
        var relative = text.StartsWith('@');
        if (relative)
        {
            text = text[1..].Trim();
            if (basePoint is null)
            {
                return false;
            }
        }

        var parts = text.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !TryParseNumber(parts[0], out var x) ||
            !TryParseNumber(parts[1], out var y))
        {
            return false;
        }

        point = relative
            ? new CadPoint(basePoint!.Value.X + x, basePoint.Value.Y + y)
            : new CadPoint(x, y);
        return true;
    }
}
