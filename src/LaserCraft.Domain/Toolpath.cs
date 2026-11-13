namespace LaserCraft.Domain;

public readonly record struct PointMm(double X, double Y);
public sealed record ToolpathSegment(PointMm From, PointMm To, int Layer, bool Travel, double Speed, bool AirAssist = false, double Power = 100)
{
    public double Length => Math.Sqrt(Math.Pow(To.X - From.X, 2) + Math.Pow(To.Y - From.Y, 2));
    public double Seconds => Length / Speed * 60;
}
public sealed class ToolpathPlan(IReadOnlyList<ToolpathSegment> segments)
{
    public IReadOnlyList<ToolpathSegment> Segments { get; } = Array.AsReadOnly(segments.ToArray());
    public double Seconds { get; } = segments.Sum(s => s.Seconds);
    public double CutLength { get; } = segments.Where(s => !s.Travel).Sum(s => s.Length);
}
public static class ToolpathPlanner
{
    public static ToolpathPlan Build(DesignDocument d, IReadOnlySet<Guid>? selected = null, Func<DesignShape, CutLayer, CancellationToken, RasterGrid>? rasterDecoder = null, CancellationToken cancellationToken = default)
    {
        d.Validate(); var segments = new List<ToolpathSegment>(); var position = new PointMm(0, 0); long fillWork = 0, rasterWork = 0;
        for (int layer = 0; layer < d.Layers.Count; layer++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var cut = d.Layers[layer]; if (!cut.Output) continue;
            var shapes = d.Shapes.Where(s => s.Layer == layer && (selected is null || selected.Contains(s.Id))).ToArray();
            foreach (var s in shapes) if (s.X < 0 || s.Y < 0 || s.X + s.Width > d.Width || s.Y + s.Height > d.Height) throw new InvalidDataException("Output geometry is outside the design bed.");
            if (shapes.Length == 0) continue;
            RasterGrid? raster = null;
            if (cut.Kind == LayerKind.Image)
            {
                if (rasterDecoder is null) throw new InvalidDataException("Raster image decoding is required for image toolpaths.");
                var overscan = (cut.Raster ?? new()).Overscan;
                if (shapes[0].X - overscan < 0 || shapes[0].X + shapes[0].Width + overscan > d.Width) throw new InvalidDataException("Raster overscan extends beyond the design bed.");
                var columns = (int)Math.Ceiling(shapes[0].Width / cut.Interval); var rows = (int)Math.Ceiling(shapes[0].Height / cut.Interval);
                if (columns > 16000 || rows > 16000 || (long)columns * rows > 4_000_000) throw new InvalidDataException("Raster exceeds axis/sample limits. Increase line interval.");
                rasterWork += (long)columns * rows * cut.Passes;
                if (rasterWork > 8_000_000) throw new InvalidDataException("Raster job exceeds the 8-million-cell planning budget. Increase line interval or reduce passes.");
                raster = rasterDecoder(shapes[0], cut, cancellationToken); raster.Validate();
                if (raster.Width != columns || raster.Height != rows) throw new InvalidDataException("Raster decoder returned a grid that does not match the image interval.");
            }
            foreach(var batch in cut.PassOrder==PassOrderMode.PerObject?shapes.Select(shape=>new[]{shape}):new[]{shapes})
            for (int pass = 0; pass < cut.Passes; pass++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (raster is not null) AppendRaster(batch[0], raster, cut.Raster ?? new());
                else if (cut.Mode == CutMode.Line)
                    foreach (var s in batch)
                    {
                        if (s.Kind == ShapeKind.Path) { foreach (var edge in VectorPaths.Edges(s)) { cancellationToken.ThrowIfCancellationRequested(); Add(edge.From, edge.To); } continue; }
                        var points = s.Kind == ShapeKind.Rectangle
                            ? new[] { new PointMm(s.X, s.Y), new(s.X + s.Width, s.Y), new(s.X + s.Width, s.Y + s.Height), new(s.X, s.Y + s.Height), new(s.X, s.Y) }
                            : Enumerable.Range(0, 129).Select(i => new PointMm(s.X + s.Width / 2 + Math.Cos(i * Math.PI / 64) * s.Width / 2, s.Y + s.Height / 2 + Math.Sin(i * Math.PI / 64) * s.Height / 2)).ToArray();
                        for (int i = 1; i < points.Length; i++) Add(points[i - 1], points[i]);
                    }
                else
                {
                    var b = ShapeEditing.Bounds(batch); var rows = Math.Ceiling(b.Height / cut.Interval);
                    if (rows > 100000) throw new InvalidDataException("Fill preview exceeds the 100000-row limit. Increase line interval.");
                    if (batch.Any(s => s.Kind == ShapeKind.Path && s.Path!.Contours.Any(c => !c.Closed))) throw new InvalidDataException("Fill requires closed contours. Close the path or choose Line mode.");
                    // Index edges once by their first/last intersected scan row. Horizontal edges never cross a row.
                    int FirstRow(double edgeY)
                    {
                        var r=Math.Clamp((int)Math.Ceiling((edgeY-b.Y)/cut.Interval-.5),0,(int)rows);
                        while(r>0&&b.Y+(r-1+.5)*cut.Interval>=edgeY)r--;
                        while(r<rows&&b.Y+(r+.5)*cut.Interval<edgeY)r++;
                        return r;
                    }
                    var events = batch.Where(s=>s.Kind==ShapeKind.Path).SelectMany(VectorPaths.Edges).Where(e=>e.From.Y!=e.To.Y)
                        .Select(e=>(A:e.From,Z:e.To,Start:FirstRow(Math.Min(e.From.Y,e.To.Y)),End:FirstRow(Math.Max(e.From.Y,e.To.Y))))
                        .Where(e=>e.Start<e.End).OrderBy(e=>e.Start).ToArray();
                    var active = new List<(PointMm A,PointMm Z,int Start,int End)>();var cursor=0;var primitives=batch.Where(s=>s.Kind!=ShapeKind.Path).ToArray();
                    for (int row = 0; row < rows; row++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var y = b.Y + (row + .5) * cut.Interval; var crossings = new List<double>();
                        active.RemoveAll(e=>e.End<=row);while(cursor<events.Length&&events[cursor].Start<=row){active.Add(events[cursor]);cursor++;}
                        fillWork+=active.Count+primitives.Length;if(fillWork>3_000_000)throw new InvalidDataException("Fill exceeds the 3-million active-intersection budget. Increase interval or simplify artwork.");
                        foreach(var edge in active) { var a=edge.A;var z=edge.Z;if((a.Y>y)!=(z.Y>y))crossings.Add(a.X+(y-a.Y)*(z.X-a.X)/(z.Y-a.Y)); }
                        foreach (var s in primitives)
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
                    Emit(from, true, 6000, 0); Emit(to, false, cut.Speed, cut.Power);
                }
                void Emit(PointMm to, bool travel, double speed, double power)
                {
                    if (position == to) return;
                    if (segments.Count >= 200000) throw new InvalidDataException("Preview exceeds the 200000-segment limit. Increase image interval or simplify artwork.");
                    segments.Add(new(position, to, layer, travel, speed, !travel && cut.AirAssist, power)); position = to;
                }
                void AppendRaster(DesignShape image, RasterGrid grid, RasterSettings options)
                {
                    var dx = image.Width / grid.Width; var dy = image.Height / grid.Height;
                    for (int row = 0; row < grid.Height; row++)
                    {
                        cancellationToken.ThrowIfCancellationRequested(); var sourceRow = image.FlipY ? grid.Height - 1 - row : row;
                        byte Darkness(int x) => grid.Darkness[sourceRow * grid.Width + (image.FlipX ? grid.Width - 1 - x : x)];
                        if (cut.Power == 0 || !Enumerable.Range(0, grid.Width).Any(x => Darkness(x) > 0)) continue;
                        var reverse = options.Bidirectional && row % 2 == 1; var y = image.Y + (row + .5) * dy;
                        Emit(new(reverse ? image.X + image.Width + options.Overscan : image.X - options.Overscan, y), true, 6000, 0);
                        var column = reverse ? grid.Width - 1 : 0;
                        while (column >= 0 && column < grid.Width)
                        {
                            var value = Darkness(column); var first = column; var step = reverse ? -1 : 1;
                            do { column += step; } while (column >= 0 && column < grid.Width && Darkness(column) == value);
                            if (value == 0) continue;
                            var startX = image.X + (reverse ? first + 1 : first) * dx; var endX = image.X + (reverse ? column + 1 : column) * dx;
                            Emit(new(startX, y), true, cut.Speed, 0); Emit(new(endX, y), false, cut.Speed, cut.Power * value / 255);
                        }
                        Emit(new(reverse ? image.X - options.Overscan : image.X + image.Width + options.Overscan, y), true, cut.Speed, 0);
                    }
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
    public void Seek(double seconds)
    {
        if(!double.IsFinite(seconds))throw new ArgumentOutOfRangeException(nameof(seconds));
        Elapsed=Math.Clamp(seconds,0,Plan.Seconds);State=Elapsed>=Plan.Seconds?SimulationState.Completed:SimulationState.Paused;
    }
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        if (State != SimulationState.Running) return;
        Elapsed = Math.Min(Plan.Seconds, Elapsed + seconds); if (Elapsed >= Plan.Seconds) State = SimulationState.Completed;
    }
}
