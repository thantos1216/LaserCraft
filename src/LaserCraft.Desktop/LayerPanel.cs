using Avalonia;
using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private readonly TextBox layerName = new();
    private readonly Button layerColor = new() { Content = "Choose color…" };
    private string layerColorValue = "#FFFFFF";
    private async Task NewLayer()
    {
        var session = Current; var dialog = new Window { Title = "New layer", Width = 430, Height = 340, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var name = new TextBox { Text = $"Layer {session.Canvas.History.Document.Layers.Count + 1}" }; var kind = new ComboBox { ItemsSource = Enum.GetValues<LayerKind>(), SelectedItem = LayerKind.Vector }; var assign = new CheckBox { Content = "Assign selected objects", IsChecked = false }; var message = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 6 }; panel.Children.Add(Label("Layer name")); panel.Children.Add(name); panel.Children.Add(kind); panel.Children.Add(assign); panel.Children.Add(message);
        var buttons = new WrapPanel(); buttons.Children.Add(Button("Create layer", () =>
        {
            try { var c = session.Canvas; var next = LayerEditing.Add(c.History.Document, name.Text ?? "", (LayerKind)kind.SelectedItem!); var index = next.Layers.Count - 1; if (assign.IsChecked == true) next = LayerEditing.Assign(next, c.Selection, index); c.ActiveLayer = index; c.Apply(next); dialog.Close(); }
            catch (InvalidDataException ex) { message.Text = ex.Message; }
        })); buttons.Children.Add(Button("Cancel", () => dialog.Close())); panel.Children.Add(buttons); dialog.Content = panel; await dialog.ShowDialog(this);
    }
    private void MoveLayer(int direction)
    {
        var c = Current.Canvas; var from = c.ActiveLayer; var visible=LayerEditing.VisibleIndices(c.History.Document).ToList();var nextIndex=visible.IndexOf(from)+direction;if(nextIndex<0||nextIndex>=visible.Count)return;var to=visible[nextIndex];
        var next = LayerEditing.Move(c.History.Document, from, to); c.ActiveLayer = to; c.Apply(next); status.Text = $"Layer moved to job step {to + 1}. Enabled layers run from top to bottom.";
    }
    private async Task ImportImage()
    {
        try
        {
            var session = Current; var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Import raster image", FileTypeFilter = [new("Raster image") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp"] }] }); if (files.Count == 0) return;
            using var stream = await files[0].OpenReadAsync(); if (stream.Length > 8_000_000) throw new InvalidDataException("Image exceeds 8 MB."); using var bytes = new MemoryStream(); await stream.CopyToAsync(bytes); var asset = await Task.Run(() => RasterImages.Import(bytes.ToArray()));
            AddImage(session, asset, Path.GetFileNameWithoutExtension(files[0].Name));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or ArgumentException) { status.Text = "Image import failed: " + ex.Message; }
    }
    private void AddImage(Session session, RasterAsset asset, string name)
    {
        var c = session.Canvas; var document = LayerEditing.Add(c.History.Document, name, LayerKind.Image); var index = document.Layers.Count - 1;
        var w = Math.Min(document.Width * .75, asset.PixelWidth * 25.4 / 96); var h = w * asset.PixelHeight / asset.PixelWidth; if (h > document.Height * .75) { w *= document.Height * .75 / h; h = document.Height * .75; }
        var shape = new DesignShape(Guid.NewGuid(), ShapeKind.Image, (document.Width - w) / 2, (document.Height - h) / 2, w, h, index, asset); c.ActiveLayer = index; c.Selected = shape.Id; c.Apply(document with { Shapes = [.. document.Shapes, shape] }); status.Text = "Original image embedded on its own image layer. Use Image settings to prepare the engraving and Preview to plan it.";
    }
}
