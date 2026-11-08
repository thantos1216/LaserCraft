using System.Collections.Immutable;

namespace LaserCraft.Domain;

public static class TraceRefinement
{
    private sealed class BudgetExceeded : Exception;
    private sealed record Edge(PointMm A, PointMm B, int Contour, int Index, int Count);
    private readonly record struct Box(double X0, double Y0, double X1, double Y1)
    {
        public bool Contains(PointMm p) => p.X >= X0 && p.X <= X1 && p.Y >= Y0 && p.Y <= Y1;
    }
    public static (TraceResult Trace, string Note) Refine(TraceResult trace, double smoothness, double optimize, CancellationToken cancellationToken = default)
    {
        new TraceTuning(Smoothness: smoothness, Optimize: optimize).Validate(); cancellationToken.ThrowIfCancellationRequested();
        if (trace.Path is null || (smoothness == 0 && optimize == 0)) return (trace, "Original pixel-edge outlines");
        var source = trace.Source; long work = 0;
        void Tick() { if ((++work & 1023) == 0) cancellationToken.ThrowIfCancellationRequested(); if (work > 2_000_000) throw new BudgetExceeded(); }
        PointMm ToPixel(PointMm p) => new((p.X - source.X) / source.Width * trace.Width, (p.Y - source.Y) / source.Height * trace.Height);
        PointMm ToWorld(PointMm p) => new(source.X + p.X / trace.Width * source.Width, source.Y + p.Y / trace.Height * source.Height);
        var original = VectorPaths.World(trace.Path).Select(c => c.Points.Select(ToPixel).ToArray()).ToArray();
        if (original.Length > 1000) return (trace, "Refinement kept original outlines: reduce contour count with Ignore less than first.");
        try
        {
            var candidate = original.Select(points =>
            {
                var next = optimize > 0 ? Simplify(points, optimize, Tick) : points;
                if (smoothness > 0)
                {
                    for (int pass = 0; pass < 2; pass++)
                    {
                        if (next.Length > 50000) throw new BudgetExceeded();
                        var rounded = new List<PointMm>();
                        for (int i = 0; i < next.Length; i++)
                        {
                            Tick(); var a = next[i]; var b = next[(i + 1) % next.Length]; var dx = b.X - a.X; var dy = b.Y - a.Y; var length = Math.Sqrt(dx * dx + dy * dy);
                            if (length < 1e-10) continue; var t = Math.Min(.25, smoothness / (2 * length));
                            rounded.Add(new(a.X + dx * t, a.Y + dy * t)); rounded.Add(new(b.X - dx * t, b.Y - dy * t));
                        }
                        next = rounded.ToArray();
                    }
                    if (optimize > 0) next = Simplify(next, optimize, Tick);
                }
                return next;
            }).ToArray();
            if (candidate.Sum(c => c.Length) > 100000) return (trace, "Refinement kept original outlines: result would exceed vertex limits.");
            var protectedContours = new HashSet<int>();
            for (int i = 0; i < original.Length; i++)
            {
                var before = Area(original[i]); var after = Area(candidate[i]);
                if (candidate[i].Length < 3 || Math.Abs(after) < Math.Abs(before) * .5 || Math.Abs(after) < 1e-8 || Math.Sign(before) != Math.Sign(after)) { candidate[i] = original[i]; protectedContours.Add(i); }
            }
            for (int pass = 0; pass < 4; pass++)
            {
                var conflicts = TopologyConflicts(original, candidate, Tick); if (conflicts.Count == 0) break;
                if (pass == 3) return (trace, "Refinement kept original outlines: crossing/nesting checks could not stabilize.");
                foreach (var i in conflicts) { candidate[i] = original[i]; protectedContours.Add(i); }
            }
            var path = VectorPaths.Create(candidate.Select(c => new PathContour(c.Select(ToWorld).ToImmutableArray(), true)), trace.Path.Layer, trace.Path.Id);
            return (trace with { Path = path }, $"Refined polylines · crossing and hole-nesting checks passed · {protectedContours.Count} delicate contours retained");
        }
        catch (BudgetExceeded) { return (trace, "Refinement kept original outlines: topology/simplification work budget reached. Reduce detail or contour count."); }
    }
    private static PointMm[] Simplify(PointMm[] points, double tolerance, Action tick)
    {
        if (points.Length < 4) return points;
        int split = 1; double farthest = -1;
        for (int i = 1; i < points.Length; i++) { tick(); var distance = Math.Pow(points[i].X - points[0].X, 2) + Math.Pow(points[i].Y - points[0].Y, 2); if (distance > farthest) { farthest = distance; split = i; } }
        var chain = points.Concat([points[0]]).ToArray(); var keep = new bool[chain.Length]; keep[0] = keep[split] = keep[^1] = true;
        var stack = new Stack<(int A, int B)>(); stack.Push((0, split)); stack.Push((split, chain.Length - 1));
        while (stack.TryPop(out var segment))
        {
            int index = -1; double maximum = tolerance;
            for (int i = segment.A + 1; i < segment.B; i++) { tick(); var distance = VectorPaths.Distance(chain[i], chain[segment.A], chain[segment.B]); if (distance > maximum) { maximum = distance; index = i; } }
            if (index < 0) continue; keep[index] = true; stack.Push((segment.A, index)); stack.Push((index, segment.B));
        }
        var result = points.Where((_, i) => keep[i]).ToArray(); return result.Length >= 3 ? result : points;
    }
    private static double Area(PointMm[] points) => Enumerable.Range(0, points.Length).Sum(i => points[i].X * points[(i + 1) % points.Length].Y - points[(i + 1) % points.Length].X * points[i].Y) / 2;
    private static Box Bounds(PointMm[] points) => new(points.Min(p => p.X), points.Min(p => p.Y), points.Max(p => p.X), points.Max(p => p.Y));
    private static HashSet<int> TopologyConflicts(PointMm[][] before, PointMm[][] after, Action tick)
    {
        var conflicts = new HashSet<int>();
        static (long, long) Key(PointMm p) => ((long)Math.Round(p.X * 1e6), (long)Math.Round(p.Y * 1e6));
        var contacts = before.SelectMany(p => p).GroupBy(Key).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet();
        var buckets = new Dictionary<(int X, int Y), List<int>>(); var edges = new List<Edge>();
        for (int ci = 0; ci < after.Length; ci++) for (int i = 0; i < after[ci].Length; i++) edges.Add(new(after[ci][i], after[ci][(i + 1) % after[ci].Length], ci, i, after[ci].Length));
        for (int i = 0; i < edges.Count; i++)
        {
            var edge = edges[i]; var seen = new HashSet<int>();
            for (int x = (int)Math.Floor(Math.Min(edge.A.X, edge.B.X) / 16); x <= (int)Math.Floor(Math.Max(edge.A.X, edge.B.X) / 16); x++)
                for (int y = (int)Math.Floor(Math.Min(edge.A.Y, edge.B.Y) / 16); y <= (int)Math.Floor(Math.Max(edge.A.Y, edge.B.Y) / 16); y++)
                {
                    tick(); if (!buckets.TryGetValue((x, y), out var bucket)) buckets[(x, y)] = bucket = [];
                    foreach (var j in bucket) if (seen.Add(j))
                    {
                        tick(); var other = edges[j];
                        if (edge.Contour == other.Contour && (Math.Abs(edge.Index - other.Index) == 1 || Math.Abs(edge.Index - other.Index) == edge.Count - 1)) continue;
                        if (!Intersects(edge.A, edge.B, other.A, other.B)) continue;
                        var shared = new[] { edge.A, edge.B }.Where(p => Near(p, other.A) || Near(p, other.B)).ToArray();
                        var collinear = Math.Abs(Cross(edge.A, edge.B, other.A)) < 1e-8 && Math.Abs(Cross(edge.A, edge.B, other.B)) < 1e-8;
                        var overlap = collinear && (Math.Abs(edge.A.X - edge.B.X) > Math.Abs(edge.A.Y - edge.B.Y)
                            ? Math.Min(Math.Max(edge.A.X, edge.B.X), Math.Max(other.A.X, other.B.X)) - Math.Max(Math.Min(edge.A.X, edge.B.X), Math.Min(other.A.X, other.B.X)) > 1e-8
                            : Math.Min(Math.Max(edge.A.Y, edge.B.Y), Math.Max(other.A.Y, other.B.Y)) - Math.Max(Math.Min(edge.A.Y, edge.B.Y), Math.Min(other.A.Y, other.B.Y)) > 1e-8);
                        if (overlap || shared.Length != 1 || !contacts.Contains(Key(shared[0]))) { conflicts.Add(edge.Contour); conflicts.Add(other.Contour); }
                    }
                    bucket.Add(i);
                }
        }
        var oldBoxes = before.Select(Bounds).ToArray(); var newBoxes = after.Select(Bounds).ToArray();
        for (int i = 0; i < before.Length; i++) for (int j = 0; j < before.Length; j++)
        {
            tick(); if (i == j || (!oldBoxes[j].Contains(before[i][0]) && !newBoxes[j].Contains(after[i][0]))) continue;
            var oldInside = Inside(before[j], before[i][0], tick); var newInside = Inside(after[j], after[i][0], tick);
            if (oldInside is not null && newInside is not null && oldInside != newInside) { conflicts.Add(i); conflicts.Add(j); }
        }
        return conflicts;
    }
    private static bool? Inside(PointMm[] polygon, PointMm p, Action tick)
    {
        bool inside = false;
        for (int i = 0; i < polygon.Length; i++) { tick(); var a = polygon[i]; var b = polygon[(i + 1) % polygon.Length]; if (VectorPaths.Distance(p, a, b) < 1e-8) return null; if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < a.X + (p.Y - a.Y) * (b.X - a.X) / (b.Y - a.Y)) inside = !inside; }
        return inside;
    }
    private static bool Near(PointMm a, PointMm b) => Math.Abs(a.X - b.X) < 1e-8 && Math.Abs(a.Y - b.Y) < 1e-8;
    private static double Cross(PointMm a, PointMm b, PointMm p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
    private static bool Intersects(PointMm a, PointMm b, PointMm c, PointMm d)
    {
        if (Math.Max(a.X, b.X) < Math.Min(c.X, d.X) - 1e-8 || Math.Max(c.X, d.X) < Math.Min(a.X, b.X) - 1e-8 || Math.Max(a.Y, b.Y) < Math.Min(c.Y, d.Y) - 1e-8 || Math.Max(c.Y, d.Y) < Math.Min(a.Y, b.Y) - 1e-8) return false;
        return Math.Sign(Cross(a, b, c)) * Math.Sign(Cross(a, b, d)) <= 0 && Math.Sign(Cross(c, d, a)) * Math.Sign(Cross(c, d, b)) <= 0;
    }
}
