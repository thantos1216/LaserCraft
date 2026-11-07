namespace LaserCraft.Domain;

public static class LayerPalette
{
    public static readonly string[] Colors = ["#323D4A", "#4169E1", "#EF4444", "#22C55E", "#E5B80B", "#F59E0B", "#06B6D4", "#D946EF", "#94A3B8", "#3749A3", "#AD273B", "#128148", "#A28B12", "#B87617", "#198AA9", "#9944B0", "#64748B", "#7685BA", "#BA7683", "#71A588", "#A5A47A", "#B7976A", "#65A6B9", "#B57DAF", "#475569", "#5E4BA0", "#A34F43", "#466C52", "#AA8520", "#388DB1"];
    public static List<CutLayer> Create() => Colors.Select((c, i) => new CutLayer($"C{i:00}", c)).ToList();
}

public readonly record struct BoundsMm(double X, double Y, double Width, double Height);
public static class ShapeEditing
{
    public static BoundsMm Bounds(IEnumerable<DesignShape> shapes)
    {
        var a = shapes.ToArray(); if (a.Length == 0) return default;
        var x = a.Min(s => s.X); var y = a.Min(s => s.Y);
        return new(x, y, a.Max(s => s.X + s.Width) - x, a.Max(s => s.Y + s.Height) - y);
    }
    public static DesignDocument Transform(DesignDocument d, IReadOnlySet<Guid> ids, Func<DesignShape, DesignShape> transform)
    {
        var result = d with { Shapes = d.Shapes.Select(s => ids.Contains(s.Id) ? transform(s) : s).ToList() }; result.Validate(); return result;
    }
    public static DesignDocument Align(DesignDocument d, IReadOnlySet<Guid> ids, string alignment)
    {
        var bounds = Bounds(d.Shapes.Where(s => ids.Contains(s.Id)));
        return Transform(d, ids, s => alignment switch
        {
            "Left" => s with { X = bounds.X },
            "Right" => s with { X = bounds.X + bounds.Width - s.Width },
            "Top" => s with { Y = bounds.Y },
            "Bottom" => s with { Y = bounds.Y + bounds.Height - s.Height },
            "Horizontal center" => s with { X = bounds.X + (bounds.Width - s.Width) / 2 },
            "Vertical center" => s with { Y = bounds.Y + (bounds.Height - s.Height) / 2 },
            _ => throw new ArgumentException("Unknown alignment.")
        });
    }
    public static DesignDocument Mirror(DesignDocument d, IReadOnlySet<Guid> ids, bool horizontal)
    {
        var b = Bounds(d.Shapes.Where(s => ids.Contains(s.Id)));
        return Transform(d, ids, s => horizontal ? s with { X = 2 * b.X + b.Width - s.X - s.Width } : s with { Y = 2 * b.Y + b.Height - s.Y - s.Height });
    }
}
