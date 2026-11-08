using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private sealed record TraceRequest(int Threshold, int MinimumArea, int Samples, bool Invert);
    private async Task TraceImage()
    {
        var session = Current; var canvas = session.Canvas; var document = canvas.History.Document;
        var image = document.Shapes.FirstOrDefault(s => s.Id == canvas.Selected && s.Kind == ShapeKind.Image) ?? document.Shapes.FirstOrDefault(s => s.Layer == canvas.ActiveLayer && s.Kind == ShapeKind.Image);
        if (image is null) { status.Text = "Select an image or its image layer before tracing."; return; }
        var fingerprint = Snapshot(session); var sourceLayer = document.Layers[image.Layer]; var sourceOptions = sourceLayer.Raster ?? new();
        var dialog = new Window { Title = "Trace image · " + sourceLayer.Name, Width = 1020, Height = 780, MinWidth = 800, MinHeight = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background, Foreground = Foreground };
        var threshold = Number(0, 255); threshold.Value = sourceOptions.Threshold;
        var minimumArea = Number(1, 262144); minimumArea.Value = 2;
        var resolution = Number(32, 512); resolution.Value = 256; resolution.Increment = 32;
        var invert = new CheckBox { Content = "Invert", IsChecked = sourceOptions.Invert, Margin = new Thickness(8) };
        var fade = new CheckBox { Content = "Fade image", IsChecked = true, Margin = new Thickness(8) };
        var points = new CheckBox { Content = "Show vertices (up to 5000)", Margin = new Thickness(8) };
        var removeSource = new CheckBox { Content = "Remove source image after tracing (undoable)", Margin = new Thickness(8) };
        var preview = new TracePreview(image); var info = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(6) };
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto"), Margin = new Thickness(16) };
        root.Children.Add(Label("Threshold outline trace · pink contours become editable vectors", 17)); Grid.SetRow(preview, 1); root.Children.Add(preview);
        var controls = new WrapPanel(); Inline(controls, "Threshold", threshold, 100); Inline(controls, "Minimum area · pixels", minimumArea, 140); Inline(controls, "Max samples / axis", resolution, 140); controls.Children.Add(invert); controls.Children.Add(fade); controls.Children.Add(points); controls.Children.Add(removeSource); Grid.SetRow(controls, 2); root.Children.Add(controls);
        Grid.SetRow(info, 3); root.Children.Add(info); var actions = new WrapPanel(); Grid.SetRow(actions, 4); root.Children.Add(actions);
        TraceResult? prepared = null; TraceRequest? preparedOptions = null; Bitmap? bitmap = null; bool busy = false, closed = false;
        var apply = Button("Create vector layer", ApplyTrace); apply.IsEnabled = false;
        using var cancellation = new CancellationTokenSource(); var token = cancellation.Token;
        TraceRequest Read() => new((int)(threshold.Value ?? 128), (int)(minimumArea.Value ?? 2), (int)(resolution.Value ?? 256), invert.IsChecked == true);
        void Stale() { apply.IsEnabled = false; info.Text = "Settings changed · Update trace to inspect the new contours."; }
        foreach (var field in new[] { threshold, minimumArea, resolution }) field.ValueChanged += (_, _) => Stale(); invert.IsCheckedChanged += (_, _) => Stale();
        fade.IsCheckedChanged += (_, _) => { preview.Fade = fade.IsChecked == true; preview.InvalidateVisual(); };
        points.IsCheckedChanged += (_, _) => { preview.ShowVertices = points.IsChecked == true; preview.InvalidateVisual(); };
        Button? update = null;
        async Task Update()
        {
            if (busy || closed) return; busy = true; apply.IsEnabled = false; if (update is not null) update.IsEnabled = false; info.Text = "Tracing…";
            try
            {
                var options = Read();
                var result = await Task.Run(() =>
                {
                    var interval = Math.Max(image.Width, image.Height) / options.Samples;
                    var tone = sourceOptions with { Mode = RasterMode.Grayscale, Invert = options.Invert };
                    var gray = RasterImages.EngravingGrid(image, sourceLayer with { Interval = interval, Raster = tone }, token);
                    // Trace the eight-bit tone sample, independent of the image layer's engraving dither mode.
                    var mask = new RasterGrid(gray.Width, gray.Height, gray.Darkness.Select(d => d > 255 - options.Threshold ? (byte)255 : (byte)0).ToArray());
                    return (Trace: ImageTracing.Trace(mask, image, options.MinimumArea, token), Gray: gray);
                }, token);
                if (closed || Read() != options) { if (!closed) Stale(); return; }
                var next = RasterImages.ProcessedPreview(result.Gray);
                preview.Source = next; preview.Path = result.Trace.Path; bitmap?.Dispose(); bitmap = next; preview.InvalidateVisual();
                prepared = result.Trace; preparedOptions = options; apply.IsEnabled = prepared.Path is not null;
                var contourCount = prepared.Path?.Path?.Contours.Length ?? 0; var vertexCount = prepared.Path?.Path?.Contours.Sum(c => c.Points.Length) ?? 0;
                info.Text = $"{prepared.Width} × {prepared.Height} samples · {contourCount} contours · {vertexCount} vertices · {prepared.RetainedPixels} foreground pixels · {prepared.RemovedComponents} speckles removed. Uses layer brightness/contrast/gamma. Creating vectors uses default Line settings and disables source layer output.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { if (!closed) info.Text = ex.Message; }
            finally { busy = false; if (!closed && update is not null) update.IsEnabled = true; }
        }
        void ApplyTrace()
        {
            if (busy || prepared?.Path is null || Read() != preparedOptions) return;
            try
            {
                if (!sessions.Contains(session) || Snapshot(session) != fingerprint) throw new InvalidDataException("The source document changed. Close and reopen tracing.");
                var next = ImageTracing.Apply(canvas.History.Document, image.Id, prepared, removeSource.IsChecked == true);
                canvas.CancelDrawing(); canvas.ActiveLayer = next.Layers.Count - 1; canvas.Selected = prepared.Path.Id; canvas.Apply(next); SetTool("Nodes"); dialog.Close();
            }
            catch (InvalidDataException ex) { info.Text = ex.Message; }
        }
        update = AsyncButton("Update trace", Update); actions.Children.Add(update); actions.Children.Add(apply); actions.Children.Add(Button("Cancel", () => dialog.Close()));
        dialog.Content = root; dialog.Opened += async (_, _) => await Update();
        dialog.Closed += (_, _) => { closed = true; cancellation.Cancel(); preview.Source = null; bitmap?.Dispose(); };
        await dialog.ShowDialog(this);
    }
}

internal sealed class TracePreview(DesignShape frame) : Control
{
    public Bitmap? Source { get; set; }
    public DesignShape? Path { get; set; }
    public bool Fade { get; set; } = true;
    public bool ShowVertices { get; set; }
    public override void Render(DrawingContext context)
    {
        base.Render(context); var scale = Math.Max(.001, Math.Min((Bounds.Width - 24) / frame.Width, (Bounds.Height - 24) / frame.Height));
        var rect = new Rect((Bounds.Width - frame.Width * scale) / 2, (Bounds.Height - frame.Height * scale) / 2, frame.Width * scale, frame.Height * scale);
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
}
