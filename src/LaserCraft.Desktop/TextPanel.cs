using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private async Task EditText(bool existing)
    {
        var session = Current; var canvas = session.Canvas; var document = canvas.History.Document; var scope = canvas.Selection.ToHashSet(); var fingerprint = Snapshot(session);
        var old = existing && scope.Count == 1 ? document.Shapes.FirstOrDefault(s => scope.Contains(s.Id) && s.Text is not null) : null;
        if (existing && old is null) { status.Text = "Select one editable text object. Converted paths no longer have text settings."; return; }
        int target = old?.Layer ?? canvas.ActiveLayer;
        if (document.Layers[target].Kind != LayerKind.Vector) { target = document.Layers.FindIndex(l => l.Kind == LayerKind.Vector); if (target < 0) { status.Text = "Create a vector layer before adding text."; return; } }
        var fonts = FontOutlines.Families(); if (fonts.Length == 0) { status.Text = "No outline fonts are available."; return; }
        var source = old?.Text ?? new TextSource("LaserCraft", fonts.FirstOrDefault(f => f == "Arial") ?? fonts[0]);
        var dialog = new Window { Title = existing ? "Edit text" : "Create text", Width = 850, Height = 730, MinWidth = 620, MinHeight = 550, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var content = new TextBox { AcceptsReturn = true, Height = 110, Text = source.Content }; var family = new ComboBox { ItemsSource = fonts.Append(source.Family).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(), SelectedItem = source.Family, MinWidth = 250 };
        var height = Number(.1m, 1000); height.Value = (decimal)source.EmHeight; height.Increment = .5m;
        var spacing = Number(.5m, 5); spacing.Value = (decimal)source.LineSpacing; spacing.Increment = .1m;
        var tolerance = Number(.001m, 1); tolerance.Value = (decimal)source.Tolerance; tolerance.Increment = .01m;
        var bold = new CheckBox { Content = "Bold", IsChecked = source.Bold }; var italic = new CheckBox { Content = "Italic", IsChecked = source.Italic };
        var preview = new TextOutlinePreview(); var info = Label("Preview text to prepare editable outlines."); DesignShape? prepared = null;
        using var lifetime = new CancellationTokenSource(); CancellationTokenSource? worker = null; Task? running = null; TextSource? activeOptions = null; bool closed = false; Button apply = null!, prepare = null!;
        TextSource Read() => new((content.Text ?? "").Replace("\r\n", "\n").Replace('\r', '\n'), family.SelectedItem as string ?? "", (double)(height.Value ?? 10), bold.IsChecked == true, italic.IsChecked == true, (double)(spacing.Value ?? 1.2m), (double)(tolerance.Value ?? .05m));
        void Stale() { if (Read() == activeOptions) return; activeOptions = null; prepared = null; apply.IsEnabled = false; preview.Shape = null; preview.InvalidateVisual(); worker?.Cancel(); info.Text = "Settings changed · use Preview text."; }
        async Task Generate()
        {
            prepare.IsEnabled = false; apply.IsEnabled = false; prepared = null; preview.Shape = null; preview.InvalidateVisual(); info.Text = "Preparing font outlines…";
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); worker = cancel;
            try
            {
                var options = Read(); activeOptions = options; var shape = await Task.Run(() => FontOutlines.Create(options, target, cancel.Token), cancel.Token);
                if (closed || cancel.IsCancellationRequested || Read() != options) return;
                prepared = shape; preview.Shape = shape; preview.InvalidateVisual(); apply.IsEnabled = true;
                info.Text = FormattableString.Invariant($"Natural ink bounds {shape.Width:0.###} × {shape.Height:0.###} mm · {shape.Path!.Contours.Length} contours · {shape.Path.Contours.Sum(c => c.Points.Length)} vertices. Cached outlines save with the project; fonts are not embedded.");
            }
            catch (OperationCanceledException) { if (!closed) info.Text = "Preview cancelled. Use Preview text after finishing changes."; }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { if (!closed) info.Text = ex.Message; }
            finally { worker = null; if (!closed) prepare.IsEnabled = true; }
        }
        void Start() { if (running is null || running.IsCompleted) running = Generate(); }
        prepare = Button("Preview text", Start);
        apply = Button(existing ? "Update text" : "Create text", () =>
        {
            if (prepared is null) return;
            try
            {
                if (!sessions.Contains(session) || Current != session || Snapshot(session) != fingerprint || !canvas.Selection.SetEquals(scope)) throw new InvalidDataException("Document or selection changed. Close and reopen text editing.");
                var candidate = existing ? prepared : prepared with { X = Math.Max(0, (document.Width - prepared.Width) / 2), Y = Math.Max(0, (document.Height - prepared.Height) / 2) };
                var next = TextEditing.Install(document, candidate, old?.Id); canvas.CancelDrawing(); canvas.ActiveLayer = target; canvas.Selected = old?.Id ?? candidate.Id; canvas.Apply(next); dialog.Close(); status.Text = "Editable text applied · one Undo step. Preview before output.";
            }
            catch (InvalidDataException ex) { info.Text = ex.Message; }
        });
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,*,Auto,Auto"), Margin = new Thickness(16) };
        root.Children.Add(Label("Basic Latin text · no complex-script shaping", 18)); Grid.SetRow(content, 1); root.Children.Add(content);
        var optionsRow = new WrapPanel(); optionsRow.Children.Add(family); optionsRow.Children.Add(bold); optionsRow.Children.Add(italic); Grid.SetRow(optionsRow, 2); root.Children.Add(optionsRow);
        var values = new WrapPanel(); Inline(values, "Font em · mm", height, 135); Inline(values, "Line spacing ×", spacing, 135); Inline(values, "Curve tolerance · mm", tolerance, 155); Grid.SetRow(values, 3); root.Children.Add(values);
        Grid.SetRow(preview, 4); root.Children.Add(preview); Grid.SetRow(info, 5); root.Children.Add(info);
        var actions = new WrapPanel(); actions.Children.Add(prepare); actions.Children.Add(apply); actions.Children.Add(Button("Cancel", () => dialog.Close())); Grid.SetRow(actions, 6); root.Children.Add(actions);
        content.TextChanged += (_, _) => Stale(); family.SelectionChanged += (_, _) => Stale(); bold.IsCheckedChanged += (_, _) => Stale(); italic.IsCheckedChanged += (_, _) => Stale(); height.ValueChanged += (_, _) => Stale(); spacing.ValueChanged += (_, _) => Stale(); tolerance.ValueChanged += (_, _) => Stale();
        dialog.Opened += (_, _) => Start(); dialog.Closed += (_, _) => { closed = true; lifetime.Cancel(); worker?.Cancel(); }; dialog.Content = root;
        await dialog.ShowDialog(this); if (running is not null) await running;
    }
}
internal sealed class TextOutlinePreview : Control
{
    public DesignShape? Shape { get; set; }
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0A111B")), new Rect(Bounds.Size));
        if (Shape is not { } shape) return;
        var scale = Math.Max(.001, Math.Min(Math.Max(1, Bounds.Width - 30) / shape.Width, Math.Max(1, Bounds.Height - 30) / shape.Height));
        Point Screen(PointMm p) => new((Bounds.Width - shape.Width * scale) / 2 + (p.X - shape.X) * scale, (Bounds.Height - shape.Height * scale) / 2 + (p.Y - shape.Y) * scale);
        var pen = new Pen(Brushes.Cyan, 1); foreach (var edge in VectorPaths.Edges(shape)) context.DrawLine(pen, Screen(edge.From), Screen(edge.To));
    }
}
