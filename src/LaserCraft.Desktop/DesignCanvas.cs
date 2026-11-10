using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed class DesignCanvas : Control
{
    private sealed record ArtworkKey(Guid Asset, double Width, double Height, double Interval, RasterSettings Settings);
    private sealed class Artwork(ArtworkKey key) { public ArtworkKey Key { get; } = key; public Avalonia.Media.Imaging.Bitmap? Bitmap { get; set; } }
    private readonly Dictionary<Guid, Artwork> bitmaps = [];
    private static readonly SemaphoreSlim imageWorkers = new(2);
    public bool ShowTravel { get; set; } = true;
    public EditorHistory History { get; } = new();
    public HashSet<Guid> Selection { get; } = [];
    public Guid? Selected { get => Selection.Count == 1 ? Selection.First() : null; set { Selection.Clear(); if (value is Guid id) Selection.Add(id); } }
    public bool ShowPreview { get; set; }
    public ToolpathPlan? Plan { get; set; }
    public double PreviewSeconds { get; set; } = double.PositiveInfinity;
    public string Tool { get; set; } = "Select";
    public bool Snap { get; set; } = true;
    public int ActiveLayer { get; set; }
    public event Action? Changed;
    public event Action<string>? Notice;
    public string NodeDescription => CurrentNode && activeNode is { } n ? $" · contour {n.Contour + 1}, node {n.Vertex + 1} / {n.Owner.Path!.Contours[n.Contour].Points.Length}" : "";
    private Point? start;
    private DesignShape? original, preview;
    private readonly List<PointMm> pathDraft = [];
    private PointMm? draftCursor;
    private int nodeContour = -1, nodeVertex = -1;
    private bool nodeDragging;
    private (DesignShape Owner, int Contour, int Vertex)? activeNode;
    private bool CurrentNode => Tool == "Nodes" && activeNode is { } n && Selected == n.Owner.Id && History.Document.Shapes.Any(s => s == n.Owner && History.Document.Layers[s.Layer].Visible);
    public string EditNode(string operation)
    {
        if (!CurrentNode || activeNode is not { } n) return "Click a node on the selected path first.";
        try
        {
            var edited = operation switch
            {
                "Insert" => PathNodeEditing.InsertAfter(n.Owner, n.Contour, n.Vertex),
                "Remove" => PathNodeEditing.Remove(n.Owner, n.Contour, n.Vertex),
                "Break" => PathNodeEditing.Break(n.Owner, n.Contour, n.Vertex),
                "Contour" => PathNodeEditing.RemoveContour(n.Owner, n.Contour, n.Vertex),
                _ => throw new InvalidDataException("Unknown node operation.")
            };
            Apply(History.Document with { Shapes = History.Document.Shapes.Select(s => s.Id == edited.Id ? edited : s).ToList() });
            activeNode = operation == "Insert" ? (edited, n.Contour, n.Vertex + 1) : null;
            Refresh(); return operation + " node edit applied · Undo restores the previous geometry.";
        }
        catch (InvalidDataException ex) { return ex.Message; }
    }
    private Vector moveDelta;
    private double zoom = 1;
    private Vector pan;
    private Point? panStart;
    private double Scale => Math.Max(0.05, Math.Min((Bounds.Width - 80) / History.Document.Width, (Bounds.Height - 80) / History.Document.Height)) * zoom;
    private Point Origin => new((Bounds.Width - History.Document.Width * Scale) / 2 + pan.X, (Bounds.Height - History.Document.Height * Scale) / 2 + pan.Y);
    public DesignCanvas() { Focusable = true; ClipToBounds = true; }
    private Point World(Point p) { var o = Origin; return new((p.X - o.X) / Scale, (p.Y - o.Y) / Scale); }
    private Point Snapped(Point p) => Snap ? new(Math.Round(p.X), Math.Round(p.Y)) : p;
    private static IBrush Brush(string color) { try { return new SolidColorBrush(Color.Parse(color)); } catch { return Brushes.Cyan; } }
    public void Refresh()
    {
        var valid = History.Document.Shapes.Select(s => s.Id).ToHashSet(); if (preview is not null) valid.Add(preview.Id);
        Selection.IntersectWith(valid); InvalidateVisual(); Changed?.Invoke();
    }
    public void Fit() { zoom = 1; pan = default; InvalidateVisual(); }
    public void CancelDrawing() { pathDraft.Clear(); draftCursor = null; preview = null; original = null; start = null; nodeContour = nodeVertex = -1; activeNode = null; InvalidateVisual(); }
    private void FinishPath(bool closed)
    {
        if (pathDraft.Count < (closed ? 3 : 2)) return;
        try { var s = VectorPaths.Create([new([.. pathDraft], closed)], ActiveLayer); Apply(History.Document with { Shapes = [.. History.Document.Shapes, s] }); Selected = s.Id; pathDraft.Clear(); draftCursor = null; Refresh(); }
        catch (InvalidDataException) { /* Keep the draft so Escape or a different finish can recover. */ }
    }
    public void Apply(DesignDocument d) { History.Apply(d); Refresh(); }
    public void ReleaseImages() { foreach (var artwork in bitmaps.Values) artwork.Bitmap?.Dispose(); bitmaps.Clear(); }
    private async void PrepareArtwork(DesignShape shape, CutLayer layer, Artwork entry)
    {
        Avalonia.Media.Imaging.Bitmap? bitmap = null;
        try
        {
            await imageWorkers.WaitAsync();
            try { bitmap = await Task.Run(() => RasterImages.ProcessedPreview(RasterImages.EngravingGrid(shape, layer with { Interval = Math.Max(layer.Interval, Math.Max(shape.Width, shape.Height) / 800) }))); }
            finally { imageWorkers.Release(); }
            if (bitmaps.TryGetValue(shape.Id, out var current) && ReferenceEquals(current, entry)) { entry.Bitmap = bitmap; bitmap = null; InvalidateVisual(); }
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { /* Placeholder remains; image settings/Preview report detailed processing errors. */ }
        finally { bitmap?.Dispose(); }
    }
    public void DeleteSelected() { if (Selection.Count > 0) { Apply(History.Document with { Shapes = History.Document.Shapes.Where(s => !Selection.Contains(s.Id)).ToList() }); Selected = null; Refresh(); } }
    public void SelectAll() { var d = History.Document; var ids = d.Shapes.Where(s => d.Layers[s.Layer].Visible).Select(s => s.Id).ToHashSet(); Selection.Clear(); Selection.UnionWith(ShapeGroups.Expand(d, ids)); Refresh(); }
    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx); ctx.FillRectangle(Brush("#0A111B"), new Rect(Bounds.Size));
        var d = History.Document; var o = Origin; var scale = Scale;
        var bed = new Rect(o, new Size(d.Width * scale, d.Height * scale));
        ctx.DrawRectangle(ShowPreview ? Brushes.White : Brush("#101E2B"), new Pen(Brush("#3B5366"), 1), bed);
        using (ctx.PushClip(bed))
        {
            var step = scale < .4 ? 50 : 10;
            var grid = new Pen(Brush("#203242"), .5);
            if (!ShowPreview)
            {
                for (double x = 0; x <= d.Width; x += step) ctx.DrawLine(grid, new(o.X + x * scale, o.Y), new(o.X + x * scale, bed.Bottom));
                for (double y = 0; y <= d.Height; y += step) ctx.DrawLine(grid, new(o.X, o.Y + y * scale), new(bed.Right, o.Y + y * scale));
                foreach (var removed in bitmaps.Keys.Where(id => !d.Shapes.Any(s => s.Id == id)).ToArray()) { bitmaps[removed].Bitmap?.Dispose(); bitmaps.Remove(removed); }
                foreach (var stored in d.Shapes.Where(s => d.Layers[s.Layer].Visible)) Draw(nodeVertex >= 0 && preview?.Id == stored.Id ? preview : original is not null && Selection.Contains(stored.Id) ? stored with { X = stored.X + moveDelta.X, Y = stored.Y + moveDelta.Y } : preview?.Id == stored.Id ? preview : stored);
                foreach (var group in d.Shapes.Where(s => Selection.Contains(s.Id) && s.GroupId is not null).GroupBy(s => s.GroupId))
                {
                    if (group.Count() < 2) continue;
                    var b = ShapeEditing.Bounds(group); var delta = original is not null && Selection.Contains(original.Id) ? moveDelta : default;
                    ctx.DrawRectangle(null, new Pen(Brushes.Cyan, 1, DashStyle.Dash), new Rect(o.X + (b.X + delta.X) * scale, o.Y + (b.Y + delta.Y) * scale, b.Width * scale, b.Height * scale).Inflate(7));
                }
                if (preview is not null && !d.Shapes.Any(s => s.Id == preview.Id)) Draw(preview);
                for (int i = 1; i < pathDraft.Count; i++) ctx.DrawLine(new Pen(Brushes.Cyan, 1.5), Screen(pathDraft[i - 1]), Screen(pathDraft[i]));
                if (pathDraft.Count > 0 && draftCursor is PointMm cursor) ctx.DrawLine(new Pen(Brushes.Cyan, 1, DashStyle.Dash), Screen(pathDraft[^1]), Screen(cursor));
                foreach (var p in pathDraft) ctx.DrawEllipse(Brushes.White, null, Screen(p), 3, 3);
            }
            if (ShowPreview && Plan is not null)
            {
                double elapsed = 0; PointMm? head = null;
                var stride = Math.Max(1, (int)Math.Ceiling(Plan.Segments.Count / 25000.0)); var segmentIndex = 0;
                foreach (var segment in Plan.Segments)
                {
                    if (elapsed >= PreviewSeconds) break;
                    var t = segment.Seconds == 0 ? 1 : Math.Clamp((PreviewSeconds - elapsed) / segment.Seconds, 0, 1);
                    var end = new PointMm(segment.From.X + (segment.To.X - segment.From.X) * t, segment.From.Y + (segment.To.Y - segment.From.Y) * t);
                    head = end;
                    if (segmentIndex++ % stride == 0 && (!segment.Travel || ShowTravel)) ctx.DrawLine(new Pen(segment.Travel ? Brush("#CBD5E1") : new SolidColorBrush(Color.FromArgb((byte)Math.Round(255 * Math.Clamp(segment.Power / 100, 0, 1)), 0, 0, 0)), segment.Travel ? .8 : 1.4, segment.Travel ? DashStyle.Dash : null), new(o.X + segment.From.X * scale, o.Y + segment.From.Y * scale), new(o.X + end.X * scale, o.Y + end.Y * scale));
                    elapsed += segment.Seconds;
                }
                if (head is PointMm h && double.IsFinite(PreviewSeconds)) { var p = new Point(o.X + h.X * scale, o.Y + h.Y * scale); ctx.DrawEllipse(Brushes.OrangeRed, null, p, 4, 4); ctx.DrawLine(new Pen(Brushes.OrangeRed, 1), new(p.X - 9, p.Y), new(p.X + 9, p.Y)); ctx.DrawLine(new Pen(Brushes.OrangeRed, 1), new(p.X, p.Y - 9), new(p.X, p.Y + 9)); }
            }
        }
        var rulerBrush = Brush("#9EB3C7");
        for (double tick = 0; tick <= d.Width; tick += 50) Text(tick.ToString("0"), o.X + tick * scale - 6, o.Y - 20);
        for (double tick = 0; tick <= d.Height; tick += 50) Text(tick.ToString("0"), o.X - 30, o.Y + tick * scale - 6);
        void Text(string text, double x, double y) => ctx.DrawText(new FormattedText(text, System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), 10, rulerBrush), new(x, y));
        Point Screen(PointMm p) => new(o.X + p.X * scale, o.Y + p.Y * scale);
        void Draw(DesignShape s)
        {
            var r = new Rect(o.X + s.X * scale, o.Y + s.Y * scale, s.Width * scale, s.Height * scale);
            var pen = new Pen(Brush(d.Layers[s.Layer].Color), 1.8);
            if (s.Kind == ShapeKind.Image)
            {
                var layer = d.Layers[s.Layer]; var key = new ArtworkKey(s.Image!.Id, s.Width, s.Height, layer.Interval, layer.Raster ?? new());
                if (!bitmaps.TryGetValue(s.Id, out var entry) || entry.Key != key) { entry?.Bitmap?.Dispose(); entry = new(key); bitmaps[s.Id] = entry; PrepareArtwork(s, layer, entry); }
                ctx.FillRectangle(Brushes.White, r);
                if (entry.Bitmap is { } bitmap) using (ctx.PushTransform(Matrix.CreateTranslation(-r.Center.X, -r.Center.Y) * Matrix.CreateScale(s.FlipX ? -1 : 1, s.FlipY ? -1 : 1) * Matrix.CreateTranslation(r.Center.X, r.Center.Y))) ctx.DrawImage(bitmap, new Rect(bitmap.Size), r);
                ctx.DrawRectangle(null, pen, r);
            }
            else if (s.Kind == ShapeKind.Path)
            {
                foreach (var edge in VectorPaths.Edges(s)) ctx.DrawLine(pen, Screen(edge.From), Screen(edge.To));
                if (Tool == "Nodes" && Selection.Contains(s.Id))
                {
                    var contours = VectorPaths.World(s);
                    for (int ci = 0; ci < contours.Length; ci++) for (int vi = 0; vi < contours[ci].Points.Length; vi++)
                    {
                        var chosen = activeNode is { } n && Selected == s.Id && n.Owner.Id == s.Id && n.Contour == ci && n.Vertex == vi && (CurrentNode || preview?.Id == s.Id);
                        ctx.DrawRectangle(chosen ? Brushes.Orange : Brushes.White, new Pen(Brushes.Cyan, 1), new Rect(Screen(contours[ci].Points[vi]) - new Vector(3, 3), new Size(6, 6)));
                    }
                }
            }
            else if (s.Kind == ShapeKind.Rectangle) ctx.DrawRectangle(null, pen, r); else ctx.DrawEllipse(null, pen, r);
            if (Selection.Contains(s.Id)) ctx.DrawRectangle(null, new Pen(Brushes.White, .8, DashStyle.Dash), r.Inflate(4));
        }
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus(); var props = e.GetCurrentPoint(this).Properties;
        if (props.IsMiddleButtonPressed) { panStart = e.GetPosition(this); e.Pointer.Capture(this); return; }
        if (!props.IsLeftButtonPressed) return;
        if (ShowPreview) return;
        start = Snapped(World(e.GetPosition(this))); var p = start.Value; original = null; preview = null; moveDelta = default;
        nodeContour = nodeVertex = -1; nodeDragging = false;
        if (Tool == "Lines")
        {
            if (p.X >= 0 && p.Y >= 0 && p.X <= History.Document.Width && p.Y <= History.Document.Height && History.Document.Layers[ActiveLayer].Kind == LayerKind.Vector)
            {
                if (pathDraft.Count >= 3 && VectorPaths.Distance(new(p.X, p.Y), pathDraft[0], pathDraft[0]) <= 6 / Scale) FinishPath(true);
                else if (e.ClickCount == 2 && pathDraft.Count >= 2) FinishPath(false);
                else if (pathDraft.Count < 10000 && (pathDraft.Count == 0 || pathDraft[^1] != new PointMm(p.X, p.Y))) pathDraft.Add(new(p.X, p.Y));
            }
            start = null; Refresh(); return;
        }
        if (Tool == "Nodes")
        {
            activeNode = null;
            var selected = History.Document.Shapes.FirstOrDefault(s => s.Id == Selected && s.Kind == ShapeKind.Path && History.Document.Layers[s.Layer].Visible);
            if (selected is not null)
            {
                var contours = VectorPaths.World(selected);
                double closest = 6 / Scale;
                var raw = World(e.GetPosition(this)); var pointer = new PointMm(raw.X, raw.Y);
                for (int ci = 0; ci < contours.Length; ci++) for (int vi = 0; vi < contours[ci].Points.Length; vi++)
                {
                    var distance = VectorPaths.Distance(pointer, contours[ci].Points[vi], contours[ci].Points[vi]);
                    if (distance <= closest) { closest = distance; nodeContour = ci; nodeVertex = vi; original = selected; preview = selected; activeNode = (selected, ci, vi); }
                }
                if (nodeVertex < 0 && e.ClickCount == 2)
                {
                    int edgeContour = -1, edgeVertex = -1; PointMm target = default; closest = 6 / Scale;
                    for (int ci = 0; ci < contours.Length; ci++)
                        for (int vi = 0; vi < contours[ci].Points.Length - (contours[ci].Closed ? 0 : 1); vi++)
                        {
                            var a = contours[ci].Points[vi]; var b = contours[ci].Points[(vi + 1) % contours[ci].Points.Length]; var distance = VectorPaths.Distance(pointer, a, b);
                            if (distance > closest) continue;
                            var dx = b.X - a.X; var dy = b.Y - a.Y; var length = dx * dx + dy * dy; if (length == 0) continue;
                            var t = Math.Clamp(((pointer.X - a.X) * dx + (pointer.Y - a.Y) * dy) / length, 0, 1);
                            closest = distance; edgeContour = ci; edgeVertex = vi; target = new(a.X + dx * t, a.Y + dy * t);
                        }
                    if (edgeContour >= 0)
                    {
                        try
                        {
                            var inserted = PathNodeEditing.InsertAfter(selected, edgeContour, edgeVertex, target);
                            Apply(History.Document with { Shapes = History.Document.Shapes.Select(s => s.Id == selected.Id ? inserted : s).ToList() });
                            activeNode = (inserted, edgeContour, edgeVertex + 1);
                        }
                        catch (InvalidDataException) { /* Vertex/work limits leave the original intact. */ }
                        start = null; Refresh(); return;
                    }
                }
            }
            if (nodeVertex < 0) Selected = History.Document.Shapes.LastOrDefault(s => s.Kind == ShapeKind.Path && History.Document.Layers[s.Layer].Visible && ShapeEditing.HitTest(History.Document, s, p.X, p.Y, 6 / Scale))?.Id;
            e.Pointer.Capture(this); Refresh(); return;
        }
        if (Tool == "Select")
        {
            original = History.Document.Shapes.LastOrDefault(s => History.Document.Layers[s.Layer].Visible && ShapeEditing.HitTest(History.Document, s, p.X, p.Y, 5 / Scale));
            var nextSelection = ShapeGroups.Select(History.Document, Selection, original?.Id, e.KeyModifiers.HasFlag(KeyModifiers.Control), e.KeyModifiers.HasFlag(KeyModifiers.Alt));
            Selection.Clear(); Selection.UnionWith(nextSelection);
            if (original is not null && !Selection.Contains(original.Id)) original = null;
        }
        else if (History.Document.Layers[ActiveLayer].Kind == LayerKind.Vector && p.X >= 0 && p.Y >= 0 && p.X <= History.Document.Width && p.Y <= History.Document.Height)
        { preview = new(Guid.NewGuid(), Tool == "Rectangle" ? ShapeKind.Rectangle : ShapeKind.Ellipse, p.X, p.Y, .1, .1, ActiveLayer); Selected = preview.Id; }
        e.Pointer.Capture(this); Refresh();
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (panStart is Point last) { var current = e.GetPosition(this); pan += current - last; panStart = current; InvalidateVisual(); return; }
        if (Tool == "Lines") { var cursor = Snapped(World(e.GetPosition(this))); draftCursor = new(cursor.X, cursor.Y); InvalidateVisual(); return; }
        if (start is not Point a) return;
        var p = Snapped(World(e.GetPosition(this)));
        if (original is not null && nodeVertex >= 0)
        {
            if (!nodeDragging && Math.Sqrt(Math.Pow(p.X - a.X, 2) + Math.Pow(p.Y - a.Y, 2)) * Scale < 3) return;
            nodeDragging = true;
            var node = VectorPaths.World(original)[nodeContour].Points[nodeVertex];
            try { preview = VectorPaths.MoveNode(original, nodeContour, nodeVertex, new(node.X + p.X - a.X, node.Y + p.Y - a.Y)); } catch (InvalidDataException) { preview = original; }
        }
        else if (original is not null) moveDelta = p - a;
        else if (preview is not null) preview = preview with { X = Math.Min(a.X, p.X), Y = Math.Min(a.Y, p.Y), Width = Math.Max(.1, Math.Abs(p.X - a.X)), Height = Math.Max(.1, Math.Abs(p.Y - a.Y)) };
        InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (Tool == "Lines") { panStart = null; e.Pointer.Capture(null); return; }
        if (nodeVertex >= 0 && preview is not null)
        {
            try { if (preview != original) History.Apply(History.Document with { Shapes = History.Document.Shapes.Select(s => s.Id == preview.Id ? preview : s).ToList() }); activeNode = (preview, nodeContour, nodeVertex); } catch (InvalidDataException) { activeNode = null; /* Invalid node moves preserve the original. */ }
        }
        else if (original is not null && moveDelta != default)
        {
            try { History.Apply(ShapeEditing.Transform(History.Document, Selection, s => s with { X = s.X + moveDelta.X, Y = s.Y + moveDelta.Y })); } catch (InvalidDataException) { /* Invalid moves leave the document unchanged. */ }
        }
        else if (preview is not null && preview != original)
        {
            var items = History.Document.Shapes.Select(s => s.Id == preview.Id ? preview : s).ToList();
            if (!items.Any(s => s.Id == preview.Id)) items.Add(preview);
            try { History.Apply(History.Document with { Shapes = items }); } catch (InvalidDataException) { Selected = null; }
        }
        preview = null; original = null; nodeContour = nodeVertex = -1; moveDelta = default; start = null; panStart = null; e.Pointer.Capture(null); Refresh();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { start = null; panStart = null; original = null; preview = null; nodeContour = nodeVertex = -1; InvalidateVisual(); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { zoom = Math.Clamp(zoom * (e.Delta.Y > 0 ? 1.12 : 1 / 1.12), .25, 8); InvalidateVisual(); e.Handled = true; }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Tool == "Lines" && e.Key == Key.Enter) { FinishPath(false); e.Handled = true; }
        else if (Tool == "Lines" && e.Key == Key.C && e.KeyModifiers == KeyModifiers.None) { FinishPath(true); e.Handled = true; }
        else if (Tool == "Lines" && e.Key == Key.Back && pathDraft.Count > 0) { pathDraft.RemoveAt(pathDraft.Count - 1); InvalidateVisual(); e.Handled = true; }
        else if (Tool == "Nodes" && e.Key == Key.Delete) { Notice?.Invoke(EditNode("Remove")); e.Handled = true; }
        else if (Tool == "Nodes" && e.Key == Key.Insert) { Notice?.Invoke(EditNode("Insert")); e.Handled = true; }
        else if (e.Key == Key.Delete) { DeleteSelected(); e.Handled = true; }
        else if (e.Key == Key.Escape) { Selected = null; CancelDrawing(); Refresh(); }
        base.OnKeyDown(e);
    }
}
