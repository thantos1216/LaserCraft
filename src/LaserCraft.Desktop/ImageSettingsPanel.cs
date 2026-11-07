using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private async Task EditImageSettings()
    {
        var session = Current; var c = session.Canvas; var d = c.History.Document;
        var shape = d.Shapes.FirstOrDefault(s => s.Id == c.Selected && s.Kind == ShapeKind.Image) ?? d.Shapes.FirstOrDefault(s => s.Layer == c.ActiveLayer && s.Kind == ShapeKind.Image);
        if (shape is null) { status.Text = "Select an image or its image layer first."; return; }
        var layerIndex = shape.Layer; var cut = d.Layers[layerIndex]; var originalOptions = cut.Raster ?? new();
        var dialog = new Window { Title = "Image preparation · " + cut.Name, Width = 1000, Height = 800, MinWidth = 780, MinHeight = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background, Foreground = Foreground };
        var imageMode = new ComboBox { ItemsSource = Enum.GetValues<RasterMode>(), SelectedItem = originalOptions.Mode, MinWidth = 170 };
        var brightness = Number(-100, 100); brightness.Value = (decimal)originalOptions.Brightness; var contrast = Number(-100, 100); contrast.Value = (decimal)originalOptions.Contrast; var gamma = Number(.1m, 5); gamma.Increment = .1m; gamma.Value = (decimal)originalOptions.Gamma;
        var threshold = Number(0, 255); threshold.Value = originalOptions.Threshold; var spacing = Number(.01m, 100); spacing.Increment = .01m; spacing.Value = (decimal)cut.Interval; var overscan = Number(0, 100); overscan.Increment = .1m; overscan.Value = (decimal)originalOptions.Overscan;
        threshold.IsEnabled = originalOptions.Mode == RasterMode.Threshold; imageMode.SelectionChanged += (_, _) => threshold.IsEnabled = imageMode.SelectedItem is RasterMode.Threshold;
        var invert = new CheckBox { Content = "Invert", IsChecked = originalOptions.Invert, Margin = new Thickness(8) }; var bidirectional = new CheckBox { Content = "Bidirectional", IsChecked = originalOptions.Bidirectional, Margin = new Thickness(8) };
        using var original = RasterImages.Preview(shape.Image!); Bitmap? processed = null; var prepared = new Image { Stretch = Stretch.Uniform }; var info = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(8) };
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto"), Margin = new Thickness(16) }; root.Children.Add(Label("Original image and prepared engraving samples · source pixels are preserved", 17));
        var images = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") }; var left = new StackPanel(); left.Children.Add(Label("Original")); left.Children.Add(new Image { Source = original, Stretch = Stretch.Uniform, Height = 330 }); images.Children.Add(left); var right = new StackPanel(); right.Children.Add(Label("Prepared (not a physical burn prediction)")); prepared.Height = 330; right.Children.Add(prepared); Grid.SetColumn(right, 1); images.Children.Add(right); Grid.SetRow(images, 1); root.Children.Add(images);
        var controls = new WrapPanel { Margin = new Thickness(0, 8) }; controls.Children.Add(imageMode); Inline(controls, "Brightness %", brightness, 100); Inline(controls, "Contrast %", contrast, 100); Inline(controls, "Gamma", gamma, 80); Inline(controls, "Threshold", threshold, 85); Inline(controls, "Interval mm", spacing, 90); Inline(controls, "Overscan mm", overscan, 95); controls.Children.Add(invert); controls.Children.Add(bidirectional); Grid.SetRow(controls, 2); root.Children.Add(controls);
        Grid.SetRow(info, 3); root.Children.Add(info); var buttons = new WrapPanel(); Grid.SetRow(buttons, 4); root.Children.Add(buttons);
        using var cancel = new CancellationTokenSource(); var token = cancel.Token; bool closed = false, busy = false;
        RasterSettings Read() => new((RasterMode)(imageMode.SelectedItem ?? RasterMode.FloydSteinberg), (double)(brightness.Value ?? 0), (double)(contrast.Value ?? 0), (double)(gamma.Value ?? 1), invert.IsChecked == true, (int)(threshold.Value ?? 128), bidirectional.IsChecked == true, (double)(overscan.Value ?? 0));
        Button? update = null;
        async Task Update()
        {
            if (busy || closed) return; busy = true; if (update is not null) update.IsEnabled = false; info.Text = "Preparing…";
            try
            {
                var options = Read(); options.Validate(); var layer = cut with { Interval = (double)(spacing.Value ?? .1m), Raster = options }; var grid = await Task.Run(() => RasterImages.EngravingGrid(shape, layer, token), token);
                var next = await Task.Run(() => RasterImages.ProcessedPreview(grid), token);
                if (closed) { next.Dispose(); return; }
                prepared.Source = next; processed?.Dispose(); processed = next;
                info.Text = $"{grid.Width} × {grid.Height} samples · effective {grid.Width / shape.Width * 25.4:0.0} × {grid.Height / shape.Height * 25.4:0.0} DPI · {options.Mode}. Update preview after changing settings.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { if (!closed) info.Text = ex.Message; }
            finally { busy = false; if (update is not null && !closed) update.IsEnabled = true; }
        }
        update = AsyncButton("Update preview", Update); buttons.Children.Add(update);
        buttons.Children.Add(Button("Apply", () =>
        {
            try { var options = Read(); options.Validate(); var doc = session.Canvas.History.Document; var layers = doc.Layers.ToList(); layers[layerIndex] = layers[layerIndex] with { Raster = options, Interval = (double)(spacing.Value ?? .1m) }; session.Canvas.Apply(doc with { Layers = layers }); dialog.Close(); }
            catch (InvalidDataException ex) { info.Text = ex.Message; }
        })); buttons.Children.Add(Button("Cancel", () => dialog.Close()));
        dialog.Content = root; dialog.Opened += async (_, _) => await Update(); dialog.Closed += (_, _) => { closed = true; cancel.Cancel(); prepared.Source = null; processed?.Dispose(); };
        await dialog.ShowDialog(this);
    }
}
