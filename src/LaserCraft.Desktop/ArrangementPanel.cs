using Avalonia;
using Avalonia.Controls;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private void Distribute(DistributionMode mode)
    {
        var canvas = Current.Canvas;
        try { var next = Arrangement.Distribute(canvas.History.Document, canvas.Selection, mode); canvas.CancelDrawing(); canvas.Apply(next); status.Text = "Distributed selected objects; outer anchors fixed · Undo restores positions."; }
        catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
    private void ReversePaths()
    {
        var canvas = Current.Canvas;
        try { var next = Arrangement.ReversePaths(canvas.History.Document, canvas.Selection); canvas.CancelDrawing(); canvas.Apply(next); status.Text = "Reversed each contour · closed start vertices retained · one Undo step."; }
        catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
    private async Task RectangularArray()
    {
        var session = Current; var canvas = session.Canvas; var document = canvas.History.Document; var scope = canvas.Selection.ToHashSet(); var fingerprint = Snapshot(session);
        if (scope.Count == 0) { status.Text = "Select artwork to repeat first."; return; }
        var dialog = new Window { Title = "Rectangular array", Width = 660, Height = 420, MinWidth = 560, MinHeight = 380, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var rows = Number(1, 100000); rows.Value = 2; var columns = Number(1, 100000); columns.Value = 3;
        var gapX = Number(0, 20000); gapX.Value = 5; gapX.Increment = .1m; var gapY = Number(0, 20000); gapY.Value = 5; gapY.Increment = .1m;
        var info = Label(""); ArrayLayout? layout = null; Button apply = null!;
        int Count(NumericUpDown input) { var v = input.Value ?? 0; if (v != decimal.Truncate(v)) throw new InvalidDataException("Rows and columns must be whole numbers."); return (int)v; }
        void Update()
        {
            layout = null; apply.IsEnabled = false;
            try
            {
                layout = Arrangement.MeasureArray(document, scope, Count(rows), Count(columns), (double)(gapX.Value ?? 0), (double)(gapY.Value ?? 0));
                info.Text = FormattableString.Invariant($"{layout.Rows} × {layout.Columns} cells including originals · adds {layout.AddedObjects} objects and {layout.AddedLayers} image layers.\nCell pitch: {layout.PitchX:0.###} × {layout.PitchY:0.###} mm; total bounds {layout.Bounds.Width:0.###} × {layout.Bounds.Height:0.###} mm.");
                if (layout.Bounds.X < 0 || layout.Bounds.Y < 0 || layout.Bounds.X + layout.Bounds.Width > document.Width || layout.Bounds.Y + layout.Bounds.Height > document.Height) info.Text += "\nArtwork extends outside the bed. Reposition it before output; Preview/export will reject off-bed paths.";
                apply.IsEnabled = true;
            }
            catch (InvalidDataException ex) { info.Text = ex.Message; }
        }
        apply = Button("Create array", () =>
        {
            if (layout is null) return;
            try
            {
                if (!sessions.Contains(session) || Current != session || Snapshot(session) != fingerprint || !canvas.Selection.SetEquals(scope)) throw new InvalidDataException("Document or selection changed. Close and reopen the array dialog.");
                var result = Arrangement.RectangularArray(document, scope, Count(rows), Count(columns), (double)(gapX.Value ?? 0), (double)(gapY.Value ?? 0));
                canvas.CancelDrawing(); canvas.Selection.Clear(); foreach (var id in result.Selection) canvas.Selection.Add(id); canvas.Apply(result.Document); dialog.Close(); status.Text = "Array created · originals and copies selected · one Undo step.";
            }
            catch (InvalidDataException ex) { info.Text = ex.Message; apply.IsEnabled = false; }
        });
        var panel = new StackPanel { Spacing = 12, Margin = new Thickness(18) }; panel.Children.Add(Label("Repeat the selected artwork as a cell", 18));
        panel.Children.Add(Label("Originals stay in the top-left cell. Copies extend right/down in row-major order. Gaps are measured between the combined selection bounds. Vector copies keep their layers; each image copy gets its own layer with copied engraving settings."));
        var counts = new WrapPanel(); Inline(counts, "Rows (total)", rows, 135); Inline(counts, "Columns (total)", columns, 135); panel.Children.Add(counts);
        var gaps = new WrapPanel(); Inline(gaps, "Horizontal gap · mm", gapX, 160); Inline(gaps, "Vertical gap · mm", gapY, 160); panel.Children.Add(gaps); panel.Children.Add(info);
        var actions = new WrapPanel(); actions.Children.Add(apply); actions.Children.Add(Button("Cancel", () => dialog.Close())); panel.Children.Add(actions);
        rows.ValueChanged += (_, _) => Update(); columns.ValueChanged += (_, _) => Update(); gapX.ValueChanged += (_, _) => Update(); gapY.ValueChanged += (_, _) => Update();
        dialog.Content = new ScrollViewer { Content = panel }; Update(); await dialog.ShowDialog(this);
    }
    private async Task RotateVectors()
    {
        var session = Current; var canvas = session.Canvas; var document = canvas.History.Document; var scope = canvas.Selection.ToHashSet(); var fingerprint = Snapshot(session);
        if (scope.Count == 0 || document.Shapes.Any(s => scope.Contains(s.Id) && s.Kind == ShapeKind.Image)) { status.Text = "Select vectors only for rotation. Raster rotation is not implemented yet."; return; }
        var dialog = new Window { Title = "Rotate selected vectors", Width = 570, Height = 310, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var angle = Number(-360, 360); angle.Value = 90; angle.Increment = 1;
        var message = Label(""); var panel = new StackPanel { Spacing = 12, Margin = new Thickness(18) };
        panel.Children.Add(Label("Rotate about the combined selection center", 18)); panel.Children.Add(Label("Positive degrees turn clockwise on the canvas. Rectangles become paths; ellipses become 128-point polylines. Path direction, holes and cut layers are retained. Images are not supported. Undo restores original shapes."));
        var value = new WrapPanel(); Inline(value, "Clockwise degrees", angle, 180); panel.Children.Add(value); panel.Children.Add(message);
        var actions = new WrapPanel(); actions.Children.Add(Button("Rotate vectors", () =>
        {
            try
            {
                if (!sessions.Contains(session) || Current != session || Snapshot(session) != fingerprint || !canvas.Selection.SetEquals(scope)) throw new InvalidDataException("Document or selection changed. Close and reopen rotation.");
                var next = Arrangement.RotateVectors(document, scope, (double)(angle.Value ?? 0)); canvas.CancelDrawing(); canvas.Apply(next); dialog.Close(); status.Text = "Vector rotation applied · one Undo step.";
            }
            catch (InvalidDataException ex) { message.Text = ex.Message; }
        })); actions.Children.Add(Button("Cancel", () => dialog.Close())); panel.Children.Add(actions); dialog.Content = new ScrollViewer { Content = panel }; await dialog.ShowDialog(this);
    }
}
