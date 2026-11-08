using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LaserCraft.Domain;

/// <summary>The straight-segment SVG path subset. Unsupported commands reject the import.</summary>
public static class SvgLinearPaths
{
    public static DesignShape Read(string data, int layer = 0, double sx = 1, double sy = 1, double ox = 0, double oy = 0)
    {
        if (string.IsNullOrWhiteSpace(data) || data.Length > 4_000_000) throw new InvalidDataException("Invalid SVG path data.");
        var tokens = new List<string>(); var offset = 0;
        var lexer = new Regex(@"\G[\s,]*([a-zA-Z]|[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)", RegexOptions.CultureInvariant);
        while (offset < data.Length)
        {
            while (offset < data.Length && (char.IsWhiteSpace(data[offset]) || data[offset] == ',')) offset++;
            if (offset == data.Length) break;
            var token = lexer.Match(data, offset); if (!token.Success) throw new InvalidDataException("Malformed SVG path token.");
            tokens.Add(token.Groups[1].Value); offset += token.Length;
            if (tokens.Count > 300000) throw new InvalidDataException("SVG path token budget exceeded.");
        }
        int i = 0, vertices = 0; char command = '\0'; var current = new PointMm(0, 0); var origin = current;
        List<PointMm>? points = null; var contours = new List<PathContour>();
        while (i < tokens.Count)
        {
            if (char.IsLetter(tokens[i][0]))
            {
                command = tokens[i++][0];
                if (command is 'Z' or 'z') { if (points is null) throw new InvalidDataException("Closepath has no contour."); Finish(true); current = origin; command = '\0'; continue; }
                if (command is not ('M' or 'm' or 'L' or 'l' or 'H' or 'h' or 'V' or 'v')) throw new InvalidDataException($"SVG path command {command} is not supported yet; no geometry imported.");
                if (i == tokens.Count || char.IsLetter(tokens[i][0])) throw new InvalidDataException("SVG command is missing coordinates.");
            }
            if (command == '\0' || (contours.Count == 0 && points is null && command is not ('M' or 'm'))) throw new InvalidDataException("SVG path must start with moveto.");
            bool relative = char.IsLower(command); var upper = char.ToUpperInvariant(command);
            var a = Number(); var b = upper is 'M' or 'L' ? Number() : 0;
            var next = upper switch { 'H' => new PointMm(relative ? current.X + a : a, current.Y), 'V' => new(current.X, relative ? current.Y + a : a), _ => new(relative ? current.X + a : a, relative ? current.Y + b : b) };
            if (upper == 'M') { if (points is not null) Finish(false); points = []; origin = next; command = relative ? 'l' : 'L'; }
            else if (points is null) points = [current];
            points!.Add(next); current = next;
            if (++vertices > 100000) throw new InvalidDataException("SVG path vertex budget exceeded.");
        }
        if (points is not null) Finish(false);
        return VectorPaths.Create(contours.Select(c => c with { Points = c.Points.Select(p => new PointMm((p.X - ox) * sx, (p.Y - oy) * sy)).ToImmutableArray() }), layer);

        double Number()
        {
            if (i >= tokens.Count || !double.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) throw new InvalidDataException("Invalid or missing SVG path coordinate.");
            return value;
        }
        void Finish(bool closed) { contours.Add(new(points!.ToImmutableArray(), closed)); points = null; }
    }
}
