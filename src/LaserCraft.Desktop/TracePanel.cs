using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private sealed record TraceRequest(TraceTuning Tuning, int MinimumArea, int Samples, bool Invert);
    private sealed class TraceParameter
    {
        public NumericUpDown Number { get; }
        public Control View { get; }
        public event Action? Changed;
        public double Value => (double)(Number.Value ?? 0);
        public TraceParameter(string name, double min, double max, double value, double step = 1)
        {
            Number = MainWindow.Number((decimal)min, (decimal)max); Number.Value = (decimal)value; Number.Increment = (decimal)step; Number.Width = 76;
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, SmallChange = step, LargeChange = step * 10, Height = 23, MinHeight = 23 };
            Avalonia.Automation.AutomationProperties.SetName(slider, name);
            var heading = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; heading.Children.Add(new TextBlock { Text = name, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }); Grid.SetColumn(Number, 1); heading.Children.Add(Number);
            var panel = new StackPanel { Spacing = 1, Margin = new Thickness(0, 3) }; panel.Children.Add(heading); panel.Children.Add(slider); View = panel;
            bool syncing = false;
            slider.ValueChanged += (_, _) => { if (syncing) return; syncing = true; Number.Value = (decimal)Math.Clamp(Math.Round(slider.Value / step) * step, min, max); slider.Value = Value; syncing = false; Changed?.Invoke(); };
            Number.ValueChanged += (_, _) => { if (syncing) return; syncing = true; slider.Value = Value; syncing = false; Changed?.Invoke(); };
        }
    }
    private async Task TraceImage()
    {
        var session = Current; var canvas = session.Canvas; var document = canvas.History.Document;
        var image = document.Shapes.FirstOrDefault(s => s.Id == canvas.Selected && s.Kind == ShapeKind.Image) ?? document.Shapes.FirstOrDefault(s => s.Layer == canvas.ActiveLayer && s.Kind == ShapeKind.Image);
        if (image is null) { status.Text = "Select an image or its image layer before tracing."; return; }
        var fingerprint = Snapshot(session); var sourceLayer = document.Layers[image.Layer]; var sourceOptions = sourceLayer.Raster ?? new();
        var dialog = new Window { Title = "Trace photograph · " + sourceLayer.Name, Width = 1180, Height = 900, MinWidth = 900, MinHeight = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background, Foreground = Foreground };
        var cutoff = new TraceParameter("Cutoff · darker limit", 0, 255, 0); var threshold = new TraceParameter("Threshold · lighter limit", 0, 255, sourceOptions.Threshold);
        var minimum = new TraceParameter("Ignore less than · pixels", 1, 1000, 8); var resolution = new TraceParameter("Max samples / axis", 64, 1024, 512, 64);
        var noise = new TraceParameter("Noise smoothing · px", 0, 3, .6, .1); var smoothing = new TraceParameter("Outline smoothness · px", 0, 2, .5, .1); var optimize = new TraceParameter("Optimize tolerance · px", 0, 2, .3, .05);
        var contrast = new TraceParameter("Sketch contrast", 0, 64, 8); var radius = new TraceParameter("Sketch radius · px", 2, 32, 8);
        var invert = new CheckBox { Content = "Invert image", IsChecked = sourceOptions.Invert }; var sketch = new CheckBox { Content = "Sketch · local contrast" };
        var fade = new CheckBox { Content = "Fade image", IsChecked = true }; var points = new CheckBox { Content = "Show vertices (up to 5000)" };
        var showMask = new CheckBox { Content = "Show threshold mask" };
        var removeSource = new CheckBox { Content = "Remove source image (undoable)" };
        var preview = new TracePreview(image) { ClipToBounds = true }; var info = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) };
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto"), Margin = new Thickness(14) };
        root.Children.Add(Label("Photograph tracing · adjust sliders, zoom to inspect, then create editable vectors", 17));
        var body = new Grid { ColumnDefinitions = new ColumnDefinitions("*,310") }; body.Children.Add(preview);
        var controls = new StackPanel { Spacing = 3, Margin = new Thickness(10, 0) };
        controls.Children.Add(Label("Live preview · updates automatically"));
        foreach (var check in new[] { invert, sketch }) controls.Children.Add(check);
        foreach (var value in new[] { cutoff, threshold, minimum, noise, smoothing, optimize, resolution, contrast, radius }) controls.Children.Add(value.View);
        foreach (var check in new[] { fade, points, showMask, removeSource }) controls.Children.Add(check);
        controls.Children.Add(Button("Fit preview", preview.Fit));
        controls.Children.Add(Label("Wheel: zoom · middle drag: pan. Uses image-layer brightness/contrast/gamma. Smooth outlines are sampled polylines; Bézier fitting is not implemented."));
        var scroll = new ScrollViewer { Content = controls }; Grid.SetColumn(scroll, 1); body.Children.Add(scroll); Grid.SetRow(body, 1); root.Children.Add(body);
        Grid.SetRow(info, 2); root.Children.Add(info); var actions = new WrapPanel(); Grid.SetRow(actions, 3); root.Children.Add(actions);
        TraceResult? prepared = null; TraceRequest? preparedOptions = null; Bitmap? bitmap = null, maskBitmap = null; bool closed = false;
        using var lifetime = new CancellationTokenSource(); using var gate = new SemaphoreSlim(1); CancellationTokenSource? running = null; var jobs = new List<Task>();
        var apply = Button("Create vector layer", ApplyTrace); apply.IsEnabled = false;
        (int Samples, bool Invert, RasterGrid Gray)? cache = null;
        var debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        TraceRequest Read() => new(new((int)cutoff.Value, (int)threshold.Value, noise.Value, smoothing.Value, optimize.Value, sketch.IsChecked == true, (int)radius.Value, contrast.Value), (int)minimum.Value, (int)resolution.Value, invert.IsChecked == true);
        void Stale()
        {
            if (closed) return; apply.IsEnabled = false; running?.Cancel(); info.Text = "Settings changed · preparing a new trace…"; debounce.Stop();
            cutoff.View.IsEnabled = threshold.View.IsEnabled = sketch.IsChecked != true; contrast.View.IsEnabled = radius.View.IsEnabled = sketch.IsChecked == true;
            debounce.Start();
        }
        foreach (var value in new[] { cutoff, threshold, minimum, noise, smoothing, optimize, resolution, contrast, radius }) value.Changed += Stale;
        invert.IsCheckedChanged += (_, _) => Stale(); sketch.IsCheckedChanged += (_, _) => Stale();
        fade.IsCheckedChanged += (_, _) => { preview.Fade = fade.IsChecked == true; preview.InvalidateVisual(); };
        points.IsCheckedChanged += (_, _) => { preview.ShowVertices = points.IsChecked == true; preview.InvalidateVisual(); };
        showMask.IsCheckedChanged += (_, _) => { preview.Source = showMask.IsChecked == true ? maskBitmap : bitmap; preview.InvalidateVisual(); };
        void StartUpdate() { debounce.Stop(); if (closed) return; running?.Cancel(); jobs.RemoveAll(t => t.IsCompleted); jobs.Add(Update()); }
        debounce.Tick += (_, _) => StartUpdate();
        async Task Update()
        {
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); var token = cancel.Token; running = cancel;
            var options = Read(); apply.IsEnabled = false; info.Text = "Preparing photographic trace…";
            try
            {
                await gate.WaitAsync(token); PhotoTraceResult result;
                try
                {
                    result = await Task.Run(() =>
                    {
                        options.Tuning.Validate();
                        if (cache is null || cache.Value.Samples != options.Samples || cache.Value.Invert != options.Invert)
                        {
                            var interval = Math.Max(image.Width, image.Height) / options.Samples;
                            var gray = RasterImages.EngravingGrid(image, sourceLayer with { Interval = interval, Raster = sourceOptions with { Mode = RasterMode.Grayscale, Invert = options.Invert } }, token);
                            token.ThrowIfCancellationRequested(); cache = (options.Samples, options.Invert, gray);
                        }
                        return PhotoTracing.Build(cache.Value.Gray, image, options.Tuning, options.MinimumArea, token);
                    }, token);
                }
                finally { gate.Release(); }
                if (closed || token.IsCancellationRequested || Read() != options) return;
                var next = RasterImages.ProcessedPreview(result.Grayscale); Bitmap? nextMask = null;
                try { nextMask = RasterImages.ProcessedPreview(result.Mask); } catch { next.Dispose(); throw; }
                preview.Source = showMask.IsChecked == true ? nextMask : next; preview.Path = result.Trace.Path;
                bitmap?.Dispose(); maskBitmap?.Dispose(); bitmap = next; maskBitmap = nextMask; preview.InvalidateVisual();
                prepared = result.Trace; preparedOptions = options; apply.IsEnabled = prepared.Path is not null;
                var contours = prepared.Path?.Path?.Contours.Length ?? 0; var vertices = prepared.Path?.Path?.Contours.Sum(c => c.Points.Length) ?? 0;
                info.Text = $"{prepared.Width} × {prepared.Height} samples · {contours} contours · {result.OriginalVertices} → {vertices} vertices · {prepared.RemovedComponents} small areas removed. {result.RefinementNote.TrimEnd('.')}. Creating vectors uses default Line settings and disables source image output.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { if (!closed && running == cancel) info.Text = ex.Message; }
            finally { if (running == cancel) running = null; }
        }
        void ApplyTrace()
        {
            if (running is not null || prepared?.Path is null || Read() != preparedOptions) return;
            try
            {
                if (!sessions.Contains(session) || Snapshot(session) != fingerprint) throw new InvalidDataException("The source document changed. Close and reopen tracing.");
                var next = ImageTracing.Apply(canvas.History.Document, image.Id, prepared, removeSource.IsChecked == true);
                canvas.CancelDrawing(); canvas.ActiveLayer = next.Layers.Count - 1; canvas.Selected = prepared.Path.Id; canvas.Apply(next); SetTool("Nodes"); dialog.Close();
            }
            catch (InvalidDataException ex) { info.Text = ex.Message; }
        }
        actions.Children.Add(apply); actions.Children.Add(Button("Cancel", () => dialog.Close()));
        dialog.Content = root; contrast.View.IsEnabled = radius.View.IsEnabled = false; dialog.Opened += (_, _) => StartUpdate();
        dialog.Closed += (_, _) => { closed = true; debounce.Stop(); lifetime.Cancel(); running?.Cancel(); preview.Source = null; bitmap?.Dispose(); maskBitmap?.Dispose(); };
        await dialog.ShowDialog(this); await Task.WhenAll(jobs);
    }
}

internal sealed class TracePreview(DesignShape frame) : Control
{
    public Bitmap? Source { get; set; }
    public DesignShape? Path { get; set; }
    public bool Fade { get; set; } = true;
    public bool ShowVertices { get; set; }
    private double zoom = 1; private Vector pan; private Point? lastPan;
    public void Fit() { zoom = 1; pan = default; InvalidateVisual(); }
    public override void Render(DrawingContext context)
    {
        base.Render(context); var scale = Math.Max(.001, Math.Min((Bounds.Width - 24) / frame.Width, (Bounds.Height - 24) / frame.Height)) * zoom;
        var rect = new Rect((Bounds.Width - frame.Width * scale) / 2 + pan.X, (Bounds.Height - frame.Height * scale) / 2 + pan.Y, frame.Width * scale, frame.Height * scale);
        context.FillRectangle(Brushes.White, rect);
        if (Source is { } bitmap)
        {
            using (context.PushOpacity(Fade ? .3 : 1))
            using (context.PushTransform(Matrix.CreateTranslation(-rect.Center.X, -rect.Center.Y) * Matrix.CreateScale(frame.FlipX ? -1 : 1, frame.FlipY ? -1 : 1) * Matrix.CreateTranslation(rect.Center.X, rect.Center.Y))) context.DrawImage(bitmap, new Rect(bitmap.Size), rect);
        }
        if (Path is not { } path) return;
        Point Screen(PointMm p) => new(rect.X + (p.X - frame.X) * scale, rect.Y + (p.Y - frame.Y) * scale);
        var pen = new Pen(Brushes.DeepPink, 1.2);
        foreach (var edge in VectorPaths.Edges(path)) context.DrawLine(pen, Screen(edge.From), Screen(edge.To));
        if (ShowVertices && path.Path!.Contours.Sum(c => c.Points.Length) <= 5000) foreach (var point in VectorPaths.World(path).SelectMany(c => c.Points)) context.DrawEllipse(Brushes.DeepPink, null, Screen(point), 2, 2);
    }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var next = Math.Clamp(zoom * (e.Delta.Y > 0 ? 1.2 : 1 / 1.2), 1, 24); var factor = next / zoom; var position = e.GetPosition(this); var center = new Point(Bounds.Width / 2, Bounds.Height / 2);
        pan = position - center - (position - center - pan) * factor; zoom = next; InvalidateVisual(); e.Handled = true;
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e) { if (e.GetCurrentPoint(this).Properties.IsMiddleButtonPressed) { if (e.ClickCount == 2) Fit(); else { lastPan = e.GetPosition(this); e.Pointer.Capture(this); } e.Handled = true; } }
    protected override void OnPointerMoved(PointerEventArgs e) { if (lastPan is Point last) { var next = e.GetPosition(this); pan += next - last; lastPan = next; InvalidateVisual(); } }
    protected override void OnPointerReleased(PointerReleasedEventArgs e) { lastPan = null; e.Pointer.Capture(null); }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) => lastPan = null;
}
