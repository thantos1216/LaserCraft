using System.Collections.Immutable;

namespace LaserCraft.Domain;

public enum PathJoinMode { MergeAtMidpoint, BridgeGap }
public sealed record PathJoinResult(DesignDocument Document, Guid JoinedId, double Gap, PointMm EndA, PointMm EndB, PathJoinMode Mode);

public static class PathJoining
{
    public static PathJoinResult Prepare(DesignDocument document, IReadOnlySet<Guid> selection, double maximumGap, PathJoinMode mode)
    {
        if (!double.IsFinite(maximumGap) || maximumGap < 0 || maximumGap > 20000 || !Enum.IsDefined(mode))
            throw new InvalidDataException("Enter a finite maximum gap between 0 and 20000 mm and a supported join method.");
        var selected = PathComposition.SelectedPaths(document, selection);
        if (selected.Any(s => s.Layer != selected[0].Layer)) throw new InvalidDataException("Assign both paths to the same layer before joining so cut settings are preserved.");
        var contours = selected.SelectMany(s => VectorPaths.World(s)).ToArray();
        if (contours.Length != 2 || contours.Any(c => c.Closed)) throw new InvalidDataException("Select exactly two open contours, in one or two path objects. Separate compound paths or break closed contours first.");
        var a = contours[0].Points; var b = contours[1].Points;
        bool reverseA = false, reverseB = false; double gap = double.PositiveInfinity;
        // Stable tie order: keep original traversal whenever equally close.
        foreach (var orientation in new[] { (A: false, B: false), (A: false, B: true), (A: true, B: false), (A: true, B: true) })
        {
            var p = orientation.A ? a[0] : a[^1]; var q = orientation.B ? b[^1] : b[0];
            var distance = Math.Sqrt(Math.Pow(p.X - q.X, 2) + Math.Pow(p.Y - q.Y, 2));
            if (distance < gap) { gap = distance; reverseA = orientation.A; reverseB = orientation.B; }
        }
        if (gap > maximumGap) throw new InvalidDataException($"Nearest endpoints are {gap:G8} mm apart, beyond the maximum gap. No geometry was changed.");
        if (reverseA) a = a.Reverse().ToImmutableArray(); if (reverseB) b = b.Reverse().ToImmutableArray();
        var endA = a[^1]; var endB = b[0]; ImmutableArray<PointMm> joined;
        if (mode == PathJoinMode.MergeAtMidpoint)
        {
            var middle = new PointMm((endA.X + endB.X) / 2, (endA.Y + endB.Y) / 2);
            joined = a.SetItem(a.Length - 1, middle).AddRange(b.Skip(1));
        }
        else joined = a.AddRange(gap == 0 ? b.Skip(1) : b);
        // Remains open, even if the outer endpoints coincide. Closing is a separate edit.
        var shape = VectorPaths.Create([new(joined)], selected[0].Layer, selected[0].Id);
        var shapes = new List<DesignShape>();
        foreach (var stored in document.Shapes)
        {
            if (stored.Id == shape.Id) shapes.Add(shape);
            else if (!selection.Contains(stored.Id)) shapes.Add(stored);
        }
        var next = document with { Shapes = shapes }; next.Validate();
        return new(next, shape.Id, gap, endA, endB, mode);
    }
}
