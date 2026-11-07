namespace LaserCraft.Domain;

public readonly record struct PointMm(double X, double Y);
public sealed record ToolpathSegment(PointMm From, PointMm To, int Layer, bool Travel, double Speed, bool AirAssist = false)
{
    public double Length => Math.Sqrt(Math.Pow(To.X - From.X, 2) + Math.Pow(To.Y - From.Y, 2));
    public double Seconds => Length / Speed * 60;
}
public sealed record ToolpathPlan(IReadOnlyList<ToolpathSegment> Segments)
{
    public double Seconds => Segments.Sum(s => s.Seconds);
    public double CutLength => Segments.Where(s => !s.Travel).Sum(s => s.Length);
}
public static class ToolpathPlanner
{
    public static ToolpathPlan Build(DesignDocument d, IReadOnlySet<Guid>? selected = null)
    {
        d.Validate(); var segments = new List<ToolpathSegment>(); var position = new PointMm(0, 0); long fillWork = 0;
        for (int layer = 0; layer < d.Layers.Count; layer++)
        {
            var cut = d.Layers[layer]; if (!cut.Output) continue;
            var shapes = d.Shapes.Where(s => s.Layer == layer && (selected is null || selected.Contains(s.Id))).ToArray();
            foreach (var s in shapes) if (s.X < 0 || s.Y < 0 || s.X + s.Width > d.Width || s.Y + s.Height > d.Height) throw new InvalidDataException("Output geometry is outside the design bed.");
            if (shapes.Length == 0) continue;
            if (shapes.Any(s => s.Kind == ShapeKind.Image)) throw new InvalidDataException("Raster engraving paths are not implemented yet. Disable image layer Output to preview vectors.");
            for (int pass = 0; pass < cut.Passes; pass++)
            {
                if (cut.Mode == CutMode.Line)
                    foreach (var s in shapes)
                    {
                        var points = s.Kind == ShapeKind.Rectangle
                            ? new[] { new PointMm(s.X, s.Y), new(s.X + s.Width, s.Y), new(s.X + s.Width, s.Y + s.Height), new(s.X, s.Y + s.Height), new(s.X, s.Y) }
                            : Enumerable.Range(0, 129).Select(i => new PointMm(s.X + s.Width / 2 + Math.Cos(i * Math.PI / 64) * s.Width / 2, s.Y + s.Height / 2 + Math.Sin(i * Math.PI / 64) * s.Height / 2)).ToArray();
                        for (int i = 1; i < points.Length; i++) Add(points[i - 1], points[i]);
                    }
                else
                {
                    var b = ShapeEditing.Bounds(shapes); var rows = Math.Ceiling(b.Height / cut.Interval);
                    if (rows > 100000) throw new InvalidDataException("Fill preview exceeds the 100000-row limit. Increase line interval.");
                    fillWork += (long)rows * shapes.Length;
                    if (fillWork > 3_000_000) throw new InvalidDataException("Fill preview is too complex for the current interactive planner. Reduce shapes, passes or scan density.");
                    for (int row = 0; row < rows; row++)
                    {
                        var y = b.Y + (row + .5) * cut.Interval; var crossings = new List<double>();
                        foreach (var s in shapes)
                        {
                            if (y < s.Y || y >= s.Y + s.Height) continue;
                            var extent = s.Width / 2;
                            if (s.Kind == ShapeKind.Ellipse) extent *= Math.Sqrt(Math.Max(0, 1 - Math.Pow((y - s.Y - s.Height / 2) / (s.Height / 2), 2)));
                            crossings.Add(s.X + s.Width / 2 - extent); crossings.Add(s.X + s.Width / 2 + extent);
                        }
                        crossings.Sort();
                        for (int i = 0; i + 1 < crossings.Count; i += 2)
                        { var a = new PointMm(crossings[i], y); var z = new PointMm(crossings[i + 1], y); if (row % 2 == 0) Add(a, z); else Add(z, a); }
                    }
                }
                void Add(PointMm from, PointMm to)
                {
                    if (segments.Count + (position != from ? 2 : 1) > 200000) throw new InvalidDataException("Preview exceeds the 200000-segment limit.");
                    if (position != from) segments.Add(new(position, from, layer, true, 6000));
                    segments.Add(new(from, to, layer, false, cut.Speed, cut.AirAssist)); position = to;
                }
            }
        }
        return new(segments);
    }
}
public enum SimulationState { Ready, Running, Paused, Completed, Stopped }
public sealed class JobSimulation(ToolpathPlan plan)
{
    public ToolpathPlan Plan { get; } = plan;
    public SimulationState State { get; private set; } = SimulationState.Ready;
    public double Elapsed { get; private set; }
    public void Start() { if (State != SimulationState.Paused) Elapsed = 0; State = Plan.Segments.Count == 0 ? SimulationState.Completed : SimulationState.Running; }
    public void Pause() { if (State == SimulationState.Running) State = SimulationState.Paused; }
    public void Stop() { Elapsed = 0; State = SimulationState.Stopped; }
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (State != SimulationState.Running) return;
        Elapsed = Math.Min(Plan.Seconds, Elapsed + seconds); if (Elapsed >= Plan.Seconds) State = SimulationState.Completed;
    }
}
