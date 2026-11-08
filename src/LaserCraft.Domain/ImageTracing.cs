using System.Collections.Immutable;

namespace LaserCraft.Domain;

public sealed record TraceResult(DesignShape? Path, int RetainedPixels, int RemovedComponents, int Width, int Height, DesignShape Source);

/// <summary>Independent pixel-edge contour tracing with four-connected foreground components.</summary>
public static class ImageTracing
{
    private readonly record struct Vertex(int X, int Y);
    private static readonly (int X, int Y)[] Directions = [(1, 0), (0, 1), (-1, 0), (0, -1)];
    public static TraceResult Trace(RasterGrid mask, DesignShape source, int minimumArea = 2, CancellationToken cancellationToken = default)
    {
        mask.Validate();
        if (mask.Width > 512 || mask.Height > 512 || minimumArea < 1 || minimumArea > 262144) throw new InvalidDataException("Tracing supports at most 512 samples per axis and a positive minimum pixel area.");
        if (source.Kind != ShapeKind.Image || !double.IsFinite(source.X) || !double.IsFinite(source.Y) || !double.IsFinite(source.Width) || !double.IsFinite(source.Height) || source.Width <= 0 || source.Height <= 0 || source.Width > 10000 || source.Height > 10000 || Math.Abs(source.X) > 10000 || Math.Abs(source.Y) > 10000) throw new InvalidDataException("Tracing requires a valid image frame.");
        var w = mask.Width; var h = mask.Height; var active = mask.Darkness.Select(p => p >= 128).ToArray();
        var visited = new bool[active.Length]; var queue = new int[active.Length]; var removed = 0; var retained = 0;
        for (int seed = 0; seed < active.Length; seed++)
        {
            if ((seed & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!active[seed] || visited[seed]) continue;
            int head = 0, tail = 1; queue[0] = seed; visited[seed] = true;
            while (head < tail)
            {
                if ((head & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                var i = queue[head++]; var x = i % w; var y = i / w;
                if (x > 0) Visit(i - 1); if (x + 1 < w) Visit(i + 1); if (y > 0) Visit(i - w); if (y + 1 < h) Visit(i + w);
            }
            if (tail < minimumArea) { removed++; for (int i = 0; i < tail; i++) active[queue[i]] = false; } else retained += tail;
            void Visit(int i) { if (active[i] && !visited[i]) { visited[i] = true; queue[tail++] = i; } }
        }
        if (retained == 0) return new(null, 0, removed, w, h, source);
        var edges = new Dictionary<Vertex, byte>(); var starts = new List<Vertex>(); var edgeCount = 0;
        bool On(int x, int y) => x >= 0 && x < w && y >= 0 && y < h && active[y * w + x];
        for (int y = 0; y < h; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < w; x++) if (On(x, y))
            {
                if (!On(x, y - 1)) Add(new(x, y), 0);
                if (!On(x + 1, y)) Add(new(x + 1, y), 1);
                if (!On(x, y + 1)) Add(new(x + 1, y + 1), 2);
                if (!On(x - 1, y)) Add(new(x, y + 1), 3);
            }
        }
        var contours = new List<PathContour>(); var vertexCount = 0; var consumed = 0;
        foreach (var start in starts) while (edges.TryGetValue(start, out var initial))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var direction = Enumerable.Range(0, 4).First(d => (initial & (1 << d)) != 0);
            var current = start; var points = new List<Vertex> { start };
            do
            {
                if ((consumed++ & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                var remaining = (byte)(edges[current] & ~(1 << direction));
                if (remaining == 0) edges.Remove(current); else edges[current] = remaining;
                var step = Directions[direction]; current = new(current.X + step.X, current.Y + step.Y);
                if (current == start) break;
                points.Add(current);
                if (!edges.TryGetValue(current, out var available)) throw new InvalidDataException("Trace boundary is not closed.");
                // Prefer a right turn to keep diagonal-only foreground contacts as distinct contours.
                var next = -1;
                foreach (var turn in new[] { 1, 0, 3, 2 }) { var candidate = (direction + turn) % 4; if ((available & (1 << candidate)) != 0) { next = candidate; break; } }
                if (next < 0) throw new InvalidDataException("Trace boundary has no continuation."); direction = next;
            } while (true);
            var corners = new List<PointMm>();
            for (int i = 0; i < points.Count; i++)
            {
                var a = points[(i + points.Count - 1) % points.Count]; var b = points[i]; var c = points[(i + 1) % points.Count];
                if ((b.X - a.X) * (c.Y - b.Y) == (b.Y - a.Y) * (c.X - b.X)) continue;
                var u = b.X / (double)w; var v = b.Y / (double)h;
                corners.Add(new(source.X + (source.FlipX ? 1 - u : u) * source.Width, source.Y + (source.FlipY ? 1 - v : v) * source.Height));
            }
            vertexCount += corners.Count;
            if (vertexCount > 100000 || contours.Count >= 10000) throw new InvalidDataException("Trace exceeds the path vertex/contour budget. Reduce resolution or remove more speckles.");
            contours.Add(new(corners.ToImmutableArray(), true));
        }
        return new(VectorPaths.Create(contours), retained, removed, w, h, source);

        void Add(Vertex vertex, int direction)
        {
            if (++edgeCount > 200000) throw new InvalidDataException("Trace exceeds the 200000 boundary-edge budget. Reduce resolution or remove more speckles.");
            if (!edges.TryGetValue(vertex, out var bits)) starts.Add(vertex);
            edges[vertex] = (byte)(bits | (1 << direction));
        }
    }
    public static DesignDocument Apply(DesignDocument document, Guid imageId, TraceResult result, bool removeSource = false)
    {
        document.Validate();
        var source = document.Shapes.SingleOrDefault(s => s.Id == imageId && s.Kind == ShapeKind.Image) ?? throw new InvalidDataException("The source image is no longer in this document.");
        if (source != result.Source) throw new InvalidDataException("The source image changed after tracing. Update the trace first.");
        var path = result.Path ?? throw new InvalidDataException("No foreground contours were traced.");
        if (path.Kind != ShapeKind.Path) throw new InvalidDataException("Trace result is not a vector path.");
        var next = LayerEditing.Add(document, "Trace · " + document.Layers[source.Layer].Name, LayerKind.Vector);
        var layers = next.Layers.ToList(); layers[source.Layer] = layers[source.Layer] with { Output = false };
        var shapes = next.Shapes.Where(s => !removeSource || s.Id != imageId).Append(path with { Layer = layers.Count - 1 }).ToList();
        next = next with { Layers = layers, Shapes = shapes }; next.Validate(); return next;
    }
}
