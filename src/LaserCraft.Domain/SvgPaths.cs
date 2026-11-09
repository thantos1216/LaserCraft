using System.Collections.Immutable;
using System.Globalization;
using System.Text.RegularExpressions;

namespace LaserCraft.Domain;

/// <summary>SVG line and Bézier data; curves become bounded-error millimetre polylines.</summary>
public static class SvgPaths
{
    public const double DefaultCurveTolerance = .05;
    public static void ValidateTolerance(double tolerance)
    {
        if (!double.IsFinite(tolerance) || tolerance < .001 || tolerance > 1) throw new InvalidDataException("SVG curve tolerance must be between 0.001 and 1 mm.");
    }
    public static DesignShape Read(string data, int layer = 0, double sx = 1, double sy = 1, double ox = 0, double oy = 0, double curveTolerance = DefaultCurveTolerance, CancellationToken cancellationToken = default)
    {
        ValidateTolerance(curveTolerance); cancellationToken.ThrowIfCancellationRequested();
        if (!double.IsFinite(sx) || !double.IsFinite(sy) || sx <= 0 || sy <= 0 || !double.IsFinite(ox) || !double.IsFinite(oy)) throw new InvalidDataException("Invalid SVG coordinate mapping.");
        if (string.IsNullOrWhiteSpace(data) || data.Length > 4_000_000) throw new InvalidDataException("Invalid SVG path data.");
        var tokens = new List<string>(); var offset = 0;
        var lexer = new Regex(@"\G[\s,]*([a-zA-Z]|[+-]?(?:\d+\.?\d*|\.\d+)(?:[eE][+-]?\d+)?)", RegexOptions.CultureInvariant);
        while (offset < data.Length)
        {
            if ((tokens.Count & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            while (offset < data.Length && (char.IsWhiteSpace(data[offset]) || data[offset] == ',')) offset++;
            if (offset == data.Length) break;
            var token = lexer.Match(data, offset); if (!token.Success) throw new InvalidDataException("Malformed SVG path token.");
            tokens.Add(token.Groups[1].Value); offset += token.Length;
            if (tokens.Count > 300000) throw new InvalidDataException("SVG path token budget exceeded.");
        }
        int i = 0, vertices = 0, curveWork = 0; char command = '\0', previous = '\0'; var current = new PointMm(0, 0); var origin = current;
        PointMm cubicControl = current, quadraticControl = current;
        List<PointMm>? points = null; var contours = new List<PathContour>();
        while (i < tokens.Count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (char.IsLetter(tokens[i][0]))
            {
                command = tokens[i++][0];
                if (command is 'Z' or 'z') { if (points is null) throw new InvalidDataException("Closepath has no contour."); Finish(true); current = origin; previous = 'Z'; command = '\0'; continue; }
                if (command is not ('M' or 'm' or 'L' or 'l' or 'H' or 'h' or 'V' or 'v' or 'C' or 'c' or 'S' or 's' or 'Q' or 'q' or 'T' or 't')) throw new InvalidDataException($"SVG path command {command} is not supported yet; no geometry imported.");
                if (i == tokens.Count || char.IsLetter(tokens[i][0])) throw new InvalidDataException("SVG command is missing coordinates.");
            }
            if (command == '\0' || (contours.Count == 0 && points is null && command is not ('M' or 'm'))) throw new InvalidDataException("SVG path must start with moveto.");
            bool relative = char.IsLower(command); var upper = char.ToUpperInvariant(command);
            PointMm Pair() { var x = Number(); var y = Number(); return new(relative ? current.X + x : x, relative ? current.Y + y : y); }
            if (upper == 'M')
            {
                var next = Pair(); if (points is not null) Finish(false); points = []; origin = current = next; Add(Map(next)); command = relative ? 'l' : 'L';
            }
            else
            {
                if (points is null) { points = []; Add(Map(current)); }
                PointMm next;
                if (upper is 'C' or 'S')
                {
                    var first = upper == 'C' ? Pair() : previous is 'C' or 'S' ? Reflect(cubicControl) : current;
                    var second = Pair(); next = Pair(); Flatten(Map(current), Map(first), Map(second), Map(next), 0); cubicControl = second;
                }
                else if (upper is 'Q' or 'T')
                {
                    var control = upper == 'Q' ? Pair() : previous is 'Q' or 'T' ? Reflect(quadraticControl) : current;
                    next = Pair(); var p = Map(current); var q = Map(control); var end = Map(next);
                    // Exact quadratic-to-cubic conversion before subdivision.
                    Flatten(p, new(p.X / 3 + q.X * (2.0 / 3), p.Y / 3 + q.Y * (2.0 / 3)), new(end.X / 3 + q.X * (2.0 / 3), end.Y / 3 + q.Y * (2.0 / 3)), end, 0); quadraticControl = control;
                }
                else
                {
                    if (upper == 'H') { var x = Number(); next = new(relative ? current.X + x : x, current.Y); }
                    else if (upper == 'V') { var y = Number(); next = new(current.X, relative ? current.Y + y : y); }
                    else next = Pair();
                    Add(Map(next));
                }
                current = next;
            }
            previous = upper;
        }
        if (points is not null) Finish(false);
        return VectorPaths.Create(contours, layer);

        PointMm Reflect(PointMm p) => new(current.X - (p.X - current.X), current.Y - (p.Y - current.Y));
        PointMm Map(PointMm p)
        {
            var mapped = new PointMm((p.X - ox) * sx, (p.Y - oy) * sy);
            if (!double.IsFinite(mapped.X) || !double.IsFinite(mapped.Y)) throw new InvalidDataException("SVG coordinate mapping overflowed."); return mapped;
        }
        double Number()
        {
            if (i >= tokens.Count || !double.TryParse(tokens[i++], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) throw new InvalidDataException("Invalid or missing SVG path coordinate."); return value;
        }
        void Add(PointMm p) { if (++vertices > 100000) throw new InvalidDataException("SVG path vertex budget exceeded."); points!.Add(p); }
        void Finish(bool closed) { if (contours.Count >= 10000) throw new InvalidDataException("SVG contour budget exceeded."); contours.Add(new(points!.ToImmutableArray(), closed)); points = null; }
        void Flatten(PointMm a, PointMm b, PointMm c, PointMm d, int depth)
        {
            if ((++curveWork & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (curveWork > 200000) throw new InvalidDataException("SVG curve subdivision work budget exceeded.");
            // Distance to the finite chord catches collinear overshoot and backtracking too.
            var flat = Math.Max(VectorPaths.Distance(b, a, d), VectorPaths.Distance(c, a, d));
            if (double.IsFinite(flat) && flat <= curveTolerance) { Add(d); return; }
            if (depth >= 24) throw new InvalidDataException("SVG curve tolerance could not be met within subdivision limits.");
            static PointMm Mid(PointMm p, PointMm q) => new(p.X / 2 + q.X / 2, p.Y / 2 + q.Y / 2);
            var ab = Mid(a, b); var bc = Mid(b, c); var cd = Mid(c, d); var abc = Mid(ab, bc); var bcd = Mid(bc, cd); var middle = Mid(abc, bcd);
            Flatten(a, ab, abc, middle, depth + 1); Flatten(middle, bcd, cd, d, depth + 1);
        }
    }
}
