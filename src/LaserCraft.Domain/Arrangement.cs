using System.Collections.Immutable;

namespace LaserCraft.Domain;

public enum DistributionMode { HorizontalCenters, VerticalCenters, HorizontalGaps, VerticalGaps }
public sealed record ArrayLayout(int Rows, int Columns, double PitchX, double PitchY, int AddedObjects, int AddedLayers, BoundsMm Bounds);
public sealed record ArrangementResult(DesignDocument Document, IReadOnlyList<Guid> Selection);
public static class Arrangement
{
    private static DesignShape[] Selected(DesignDocument d, IReadOnlySet<Guid> ids, int minimum = 1)
    {
        d.Validate(); var selected = d.Shapes.Where(s => ids.Contains(s.Id)).ToArray();
        if (selected.Length < minimum || selected.Length != ids.Count) throw new InvalidDataException($"Select at least {minimum} existing objects.");
        return selected;
    }
    public static ArrayLayout MeasureArray(DesignDocument d, IReadOnlySet<Guid> ids, int rows, int columns, double gapX, double gapY)
    {
        var selected = Selected(d, ids);
        if (rows < 1 || columns < 1 || rows > 100000 || columns > 100000 || (long)rows * columns > 100000 || (long)rows * columns == 1 || !double.IsFinite(gapX) || !double.IsFinite(gapY) || gapX < 0 || gapY < 0)
            throw new InvalidDataException("Use at least two cells, positive row/column counts and finite nonnegative gaps; the project permits at most 100000 objects.");
        var copies = (long)rows * columns - 1; var (added, layers) = CheckCopyBudget(d, selected, copies);
        var b = ShapeEditing.Bounds(selected); var px = b.Width + gapX; var py = b.Height + gapY;
        if ((columns > 1 && px <= 0) || (rows > 1 && py <= 0)) throw new InvalidDataException("Repeated axes need a nonzero cell pitch. Add a gap for zero-extent paths.");
        var dx = (columns - 1) * px; var dy = (rows - 1) * py;
        if (!double.IsFinite(dx) || !double.IsFinite(dy) || selected.Any(s => s.X + dx > 10000 || s.Y + dy > 10000)) throw new InvalidDataException("Array positions exceed the project coordinate range.");
        return new(rows, columns, px, py, (int)added, (int)layers, new(b.X, b.Y, b.Width + dx, b.Height + dy));
    }
    internal static (int Objects,int Layers) CheckCopyBudget(DesignDocument d, DesignShape[] selected, long copies, bool rotate = false)
    {
        var added = copies * selected.Length;
        if (copies < 1 || d.Shapes.Count + added > 100000) throw new InvalidDataException("Array exceeds the 100000 object limit.");
        var images = selected.Where(s => s.Kind == ShapeKind.Image).ToArray(); var layers = copies * images.Length;
        if (d.Layers.Count + layers > 256 || d.Shapes.Count(s => s.Kind == ShapeKind.Image) + layers > 128) throw new InvalidDataException("Each image copy needs a layer; the array exceeds 256 layers or 128 images.");
        long Vertices(DesignShape s) => s.Path?.Contours.Sum(c => (long)c.Points.Length) ?? 0;
        long CopyVertices(DesignShape s) => rotate ? s.Kind == ShapeKind.Rectangle ? 4 : s.Kind == ShapeKind.Ellipse ? 128 : Vertices(s) : Vertices(s);
        if (d.Shapes.Sum(Vertices) + copies * selected.Sum(CopyVertices) > 200000) throw new InvalidDataException("Array exceeds the 200000 project path-vertex budget.");
        if (d.Shapes.Sum(s => (long)(s.Image?.DataBase64.Length ?? 0)) + copies * images.Sum(s => (long)s.Image!.DataBase64.Length) > 24000000) throw new InvalidDataException("Array exceeds the embedded image data budget.");
        return ((int)added, (int)layers);
    }
    public static ArrangementResult RectangularArray(DesignDocument d, IReadOnlySet<Guid> ids, int rows, int columns, double gapX, double gapY)
    {
        var layout = MeasureArray(d, ids, rows, columns, gapX, gapY); var selected = d.Shapes.Where(s => ids.Contains(s.Id)).ToArray();
        var shapes = new List<DesignShape>(d.Shapes.Count + layout.AddedObjects); shapes.AddRange(d.Shapes); var layers = d.Layers.ToList(); var selection = selected.Select(s => s.Id).ToList();
        for (int row = 0; row < rows; row++) for (int column = 0; column < columns; column++)
        {
            if (row == 0 && column == 0) continue;
            var cell = new List<DesignShape>(selected.Length);
            foreach (var source in selected)
            {
                var layer = source.Layer;
                if (source.Kind == ShapeKind.Image) { layer = layers.Count; layers.Add(d.Layers[source.Layer] with { Name = d.Layers[source.Layer].Name + $" [{row + 1},{column + 1}]" }); }
                var copy = source with { Id = Guid.NewGuid(), X = source.X + column * layout.PitchX, Y = source.Y + row * layout.PitchY, Layer = layer };
                cell.Add(copy); selection.Add(copy.Id);
            }
            shapes.AddRange(ShapeGroups.RemapCopies(cell));
        }
        var next = d with { Shapes = shapes, Layers = layers }; next.Validate(); return new(next, selection.AsReadOnly());
    }
    public static DesignDocument Distribute(DesignDocument d, IReadOnlySet<Guid> ids, DistributionMode mode)
    {
        Selected(d, ids, 3); var selected = ShapeGroups.Units(d, ids); if (selected.Count < 3) throw new InvalidDataException("Select at least three groups or ungrouped objects to distribute.");
        if (!Enum.IsDefined(mode)) throw new InvalidDataException("Unknown distribution mode.");
        bool horizontal = mode is DistributionMode.HorizontalCenters or DistributionMode.HorizontalGaps;
        bool gaps = mode is DistributionMode.HorizontalGaps or DistributionMode.VerticalGaps;
        double Size(ShapeUnit s) => horizontal ? s.Bounds.Width : s.Bounds.Height;
        double Start(ShapeUnit s) => horizontal ? s.Bounds.X : s.Bounds.Y;
        double Key(ShapeUnit s) => Start(s) + (gaps ? 0 : Size(s) / 2);
        var ordered = selected.OrderBy(Key).ToArray(); var first = ordered[0]; var last = ordered[^1]; var positions = new Dictionary<Guid, double>();
        double step = gaps ? (Start(last) + Size(last) - Start(first) - ordered.Sum(Size)) / (ordered.Length - 1) : (Key(last) - Key(first)) / (ordered.Length - 1);
        if (gaps && step < 0) throw new InvalidDataException("Not enough space for nonnegative equal gaps with the outer objects fixed.");
        var cursor = Start(first) + Size(first) + step;
        for (int i = 1; i < ordered.Length - 1; i++)
        {
            var delta = (gaps ? cursor : Key(first) + i * step - Size(ordered[i]) / 2) - Start(ordered[i]);
            foreach (var member in ordered[i].Members) positions[member.Id] = delta;
            cursor += Size(ordered[i]) + step;
        }
        var next = d with { Shapes = d.Shapes.Select(s => positions.TryGetValue(s.Id, out var value) ? horizontal ? s with { X = s.X + value } : s with { Y = s.Y + value } : s).ToList() }; next.Validate(); return next;
    }
    public static DesignDocument RotateVectors(DesignDocument d, IReadOnlySet<Guid> ids, double clockwiseDegrees)
    {
        var selected = Selected(d, ids);
        if (!double.IsFinite(clockwiseDegrees) || Math.Abs(clockwiseDegrees) > 360 || clockwiseDegrees % 360 == 0) throw new InvalidDataException("Choose a nonzero rotation between -360 and 360 degrees.");
        if (selected.Any(s => s.Kind == ShapeKind.Image)) throw new InvalidDataException("Rotation currently accepts vectors only. Images retain their original editable raster geometry.");
        long Count(DesignShape s) => s.Path?.Contours.Sum(c => (long)c.Points.Length) ?? 0;
        var vertices = d.Shapes.Where(s => !ids.Contains(s.Id)).Sum(Count) + selected.Sum(s => s.Kind == ShapeKind.Rectangle ? 4 : s.Kind == ShapeKind.Ellipse ? 128 : Count(s));
        if (vertices > 200000) throw new InvalidDataException("Rotation would exceed the project path-vertex budget.");
        var b = ShapeEditing.Bounds(selected); var cx = b.X + b.Width / 2; var cy = b.Y + b.Height / 2; var angle = clockwiseDegrees * Math.PI / 180;
        var cos = Math.Cos(angle); var sin = Math.Sin(angle);
        if (clockwiseDegrees % 90 == 0) { cos = Math.Round(cos); sin = Math.Round(sin); }
        PointMm Rotate(PointMm p) => new(cx + (p.X - cx) * cos - (p.Y - cy) * sin, cy + (p.X - cx) * sin + (p.Y - cy) * cos);
        var replacements = selected.ToDictionary(s => s.Id, s => VectorPaths.Create(VectorPaths.World(VectorPaths.Convert(s)).Select(c => c with { Points = c.Points.Select(Rotate).ToImmutableArray() }), s.Layer, s.Id, s.GroupId));
        var next = d with { Shapes = d.Shapes.Select(s => replacements.GetValueOrDefault(s.Id, s)).ToList() }; next.Validate(); return next;
    }
    public static DesignDocument ReversePaths(DesignDocument d, IReadOnlySet<Guid> ids)
    {
        var selected = PathComposition.SelectedPaths(d, ids);
        var replacements = selected.ToDictionary(s => s.Id, s => s with { Text = null, Path = new(s.Path!.Contours.Select(c => c with { Points = c.Closed ? c.Points.Take(1).Concat(c.Points.Skip(1).Reverse()).ToImmutableArray() : c.Points.Reverse().ToImmutableArray() }).ToImmutableArray()) });
        var next = d with { Shapes = d.Shapes.Select(s => replacements.GetValueOrDefault(s.Id, s)).ToList() }; next.Validate(); return next;
    }
}
