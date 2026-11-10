using System.Collections.Immutable;

namespace LaserCraft.Domain;

public sealed record PathContour(ImmutableArray<PointMm> Points, bool Closed = false);
public sealed record PathData(ImmutableArray<PathContour> Contours);

/// <summary>Immutable normalized polylines, shared by editing, import and output.</summary>
public static class VectorPaths
{
    public static DesignShape Create(IEnumerable<PathContour> contours, int layer = 0, Guid? id = null, Guid? groupId = null)
    {
        var paths = contours.ToImmutableArray();
        if (paths.IsEmpty || paths.Any(c => c is null || c.Points.IsDefaultOrEmpty)) throw new InvalidDataException("A path needs contours and vertices.");
        var points = paths.SelectMany(c => c.Points).ToArray();
        if (points.Length > 100000 || points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) throw new InvalidDataException("Invalid or excessive path vertices.");
        var x = points.Min(p => p.X); var y = points.Min(p => p.Y); var w = points.Max(p => p.X) - x; var h = points.Max(p => p.Y) - y;
        var data = new PathData(paths.Select(c => c with { Points = c.Points.Select(p => new PointMm(w == 0 ? 0 : (p.X - x) / w, h == 0 ? 0 : (p.Y - y) / h)).ToImmutableArray() }).ToImmutableArray());
        if (groupId == Guid.Empty) throw new InvalidDataException("Group ID must be nonempty or absent.");
        var shape = new DesignShape(id ?? Guid.NewGuid(), ShapeKind.Path, x, y, w, h, layer, Path: data, GroupId: groupId); Validate(shape); return shape;
    }
    public static void Validate(DesignShape shape)
    {
        var data = shape.Path ?? throw new InvalidDataException("Path has no vertices.");
        if (data.Contours.IsDefaultOrEmpty || data.Contours.Length > 10000) throw new InvalidDataException("Invalid path contour count.");
        long count = 0;
        foreach (var contour in data.Contours)
        {
            if (contour is null || contour.Points.IsDefault || contour.Points.Length < (contour.Closed ? 3 : 2)) throw new InvalidDataException("Open paths need two vertices; closed contours need three.");
            count += contour.Points.Length;
            if (count > 100000 || contour.Points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.X < 0 || p.X > 1 || p.Y < 0 || p.Y > 1)) throw new InvalidDataException("Invalid normalized path coordinates or vertex limit.");
        }
        if (shape.Width == 0 && shape.Height == 0) throw new InvalidDataException("A path must have nonzero extent.");
    }
    public static ImmutableArray<PathContour> World(DesignShape s) => s.Path!.Contours.Select(c => c with
    {
        Points = c.Points.Select(p => new PointMm(s.X + (s.FlipX ? 1 - p.X : p.X) * s.Width, s.Y + (s.FlipY ? 1 - p.Y : p.Y) * s.Height)).ToImmutableArray()
    }).ToImmutableArray();
    public static IEnumerable<(PointMm From, PointMm To)> Edges(DesignShape s)
    {
        foreach (var contour in World(s))
        {
            for (int i = 1; i < contour.Points.Length; i++) yield return (contour.Points[i - 1], contour.Points[i]);
            if (contour.Closed) yield return (contour.Points[^1], contour.Points[0]);
        }
    }
    public static DesignShape MoveNode(DesignShape s, int contour, int vertex, PointMm target)
    {
        var paths = World(s); var c = paths[contour]; paths = paths.SetItem(contour, c with { Points = c.Points.SetItem(vertex, target) });
        return Create(paths, s.Layer, s.Id, s.GroupId);
    }
    public static DesignShape Convert(DesignShape s) => s.Kind switch
    {
        ShapeKind.Path => s with { Text = null },
        ShapeKind.Rectangle => Create([new([new(s.X, s.Y), new(s.X + s.Width, s.Y), new(s.X + s.Width, s.Y + s.Height), new(s.X, s.Y + s.Height)], true)], s.Layer, s.Id, s.GroupId),
        ShapeKind.Ellipse => Create([new(Enumerable.Range(0, 128).Select(i => new PointMm(s.X + s.Width / 2 + Math.Cos(i * Math.PI / 64) * s.Width / 2, s.Y + s.Height / 2 + Math.Sin(i * Math.PI / 64) * s.Height / 2)).ToImmutableArray(), true)], s.Layer, s.Id, s.GroupId),
        _ => throw new InvalidDataException("Images must be traced before they can become vector paths.")
    };
    public static double Distance(PointMm p, PointMm a, PointMm b)
    {
        var dx = b.X - a.X; var dy = b.Y - a.Y; var length = dx * dx + dy * dy;
        var t = length == 0 ? 0 : Math.Clamp(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / length, 0, 1);
        return Math.Sqrt(Math.Pow(p.X - a.X - t * dx, 2) + Math.Pow(p.Y - a.Y - t * dy, 2));
    }
    public static bool Contains(DesignShape s, PointMm p)
    {
        var inside = false;
        foreach (var (a, b) in Edges(s)) if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y)) inside = !inside;
        return inside;
    }
}
