using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using LaserCraft.Domain;
using System.Text.Json;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow : Window
{
    private readonly TabControl tabs = new();
    private readonly List<Session> sessions = [];
    private readonly TextBlock status = new(), selection = new();
    private readonly ComboBox layer = new();
    private bool updatingInspector;
    private readonly List<TextBlock> accentLabels = [];
    private readonly NumericUpDown x = Number(), y = Number(), width = Number(.1m), height = Number(.1m), speed = Number(1, 100000), power = Number(0, 100);
    private readonly StackPanel help = new() { Spacing = 8 };
    private static readonly string StateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LaserCraft");
    private readonly string settingsPath = Path.Combine(StateFolder, "settings.json");
    private Session Current => sessions[Math.Clamp(tabs.SelectedIndex, 0, sessions.Count - 1)];
    private sealed class Session { public DesignCanvas Canvas { get; } = new(); public string? Path { get; set; } public string Saved { get; set; } = ""; public TabItem Tab { get; } = new(); }
    private static NumericUpDown Number(decimal min = -10000, decimal max = 10000) => new() { Minimum = min, Maximum = max, Increment = 1, FormatString = "0.###", Value = min > 0 ? min : 0, ShowButtonSpinner = false, Padding = new Thickness(5, 3), HorizontalAlignment = HorizontalAlignment.Stretch };
    private static TextBlock Label(string text, double size = 12) => new() { Text = text, FontSize = size, Margin = new Thickness(0, 8, 0, 5), TextWrapping = TextWrapping.Wrap };
    private static Button Button(string label, Action action) { var b = new Button { Content = label, Margin = new Thickness(2), Padding = new Thickness(9, 4), MinHeight = 28, FontSize = 12 }; b.Click += (_, _) => action(); return b; }
    private static Button AsyncButton(string label, Func<Task> action) { var b = Button(label, () => { }); b.Click += async (_, _) => await action(); return b; }
    public MainWindow(string? startupPath = null)
    {
        BuildWorkspace();
        tabs.SelectionChanged += (_, _) => { if (sessions.Count > 0) Refresh(); };
        try { if (startupPath is not null) AddDocument(ProjectFile.Load(startupPath), Path.GetFullPath(startupPath)); else AddDocument(DesignDocument.Empty()); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException) { AddDocument(DesignDocument.Empty()); status.Text = "Could not open startup project: " + ex.Message; }
        var helpPath = Path.Combine(AppContext.BaseDirectory, "Help.md");
        foreach (var paragraph in (File.Exists(helpPath) ? File.ReadAllText(helpPath) : "Help document was not included in this build.").Replace("\r", "").Split("\n\n"))
        {
            var heading = paragraph.StartsWith('#'); var block = Label(paragraph.TrimStart('#', ' '), heading ? 18 : 13);
            if (heading) { block.FontWeight = FontWeight.SemiBold; accentLabels.Add(block); }
            help.Children.Add(block);
        }
        LoadSettings(); KeyDown += OnShortcut;
        Closing += (_, e) => { if (!allowClose && sessions.Any(IsDirty)) { e.Cancel = true; _ = ConfirmClose(); } };
    }
    private bool allowClose;
    private async Task ConfirmClose()
    {
        if (allowClose) return;
        var dialog = new Window { Title = "Unsaved projects", Width = 420, Height = 180, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
        var panel = new StackPanel { Margin = new Thickness(24) }; panel.Children.Add(Label("There are unsaved projects. Close and discard changes?", 15));
        var buttons = new WrapPanel(); buttons.Children.Add(Button("Keep editing", () => dialog.Close(false))); buttons.Children.Add(Button("Discard and close", () => dialog.Close(true))); panel.Children.Add(buttons); dialog.Content = panel;
        if (await dialog.ShowDialog<bool>(this)) { allowClose = true; Close(); }
    }
    private static string Snapshot(Session s) => JsonSerializer.Serialize(s.Canvas.History.Document);
    private static bool IsDirty(Session s) => Snapshot(s) != s.Saved;
    private void AddDocument(DesignDocument d, string? path = null)
    {
        d.Validate(); RasterImages.Verify(d);
        var s = new Session { Path = path }; s.Canvas.History.Reset(d); s.Saved = Snapshot(s); s.Tab.Content = s.Canvas;
        s.Canvas.Changed += Refresh; sessions.Add(s); tabs.Items.Add(s.Tab); tabs.SelectedIndex = sessions.Count - 1; Refresh();
    }
    private void Refresh()
    {
        foreach (var s in sessions) s.Tab.Header = s.Canvas.History.Document.Name + (IsDirty(s) ? " •" : "");
        var c = Current.Canvas; var d = c.History.Document; var shape = d.Shapes.FirstOrDefault(s => s.Id == c.Selected);
        var present = d.Shapes.Select(s => s.Id).ToHashSet(); c.Selection.RemoveWhere(id => !present.Contains(id));
        updatingInspector = true; c.ActiveLayer = Math.Clamp(c.ActiveLayer, 0, d.Layers.Count - 1); layer.ItemsSource = d.Layers.Select((l, i) => $"{i:00}  {l.Name}").ToArray(); layer.SelectedIndex = c.ActiveLayer; updatingInspector = false;
        selection.Text = shape is null ? "Select an object to edit its dimensions." : $"{shape.Kind} · layer {shape.Layer + 1}";
        if (c.Selection.Count > 0) { var bounds = ShapeEditing.Bounds(d.Shapes.Where(s => c.Selection.Contains(s.Id))); x.Value = (decimal)bounds.X; y.Value = (decimal)bounds.Y; width.Value = (decimal)bounds.Width; height.Value = (decimal)bounds.Height; }
        RefreshWorkspace();
        status.Text = $"{d.Name}  ·  {d.Width:0} × {d.Height:0} mm  ·  {d.Shapes.Count} objects  ·  {c.Tool}  ·  {(monitor is null ? "Disconnected" : "Read-only monitor connected")}"; RefreshLayer();
    }
    private void RefreshLayer() { var i = Math.Max(0, layer.SelectedIndex); var l = Current.Canvas.History.Document.Layers[i]; speed.Value = (decimal)l.Speed; power.Value = (decimal)l.Power; passes.Value = l.Passes; interval.Value = (decimal)l.Interval; mode.ItemsSource = l.Kind == LayerKind.Image ? new[] { "Image" } : new[] { "Line", "Fill" }; mode.SelectedIndex = l.Kind == LayerKind.Image ? 0 : (int)l.Mode; layerName.Text = l.Name; layerColor.Text = l.Color; air.IsChecked = l.AirAssist; output.IsChecked = l.Output; visible.IsChecked = l.Visible; }
    private void ApplyGeometry()
    {
        try { var c = Current.Canvas; var d = c.History.Document; if (c.Selection.Count == 0) return; var b = ShapeEditing.Bounds(d.Shapes.Where(s => c.Selection.Contains(s.Id))); var sx = (double)(width.Value ?? 1) / b.Width; var sy = (double)(height.Value ?? 1) / b.Height; c.Apply(ShapeEditing.Transform(d, c.Selection, s => s with { X = (double)(x.Value ?? 0) + (s.X - b.X) * sx, Y = (double)(y.Value ?? 0) + (s.Y - b.Y) * sy, Width = s.Width * sx, Height = s.Height * sy })); }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    private void ApplyLayer()
    {
        try { var c = Current.Canvas; var d = c.History.Document; var i = Math.Max(0, layer.SelectedIndex); var layers = d.Layers.ToList(); layers[i] = layers[i] with { Speed = (double)(speed.Value ?? 1200), Power = (double)(power.Value ?? 20), Passes = (int)(passes.Value ?? 1), Interval = (double)(interval.Value ?? .1m), Name = layerName.Text ?? "", Color = layerColor.Text ?? "", Mode = layers[i].Kind == LayerKind.Image ? CutMode.Image : (CutMode)Math.Max(0, mode.SelectedIndex), AirAssist = air.IsChecked == true, Output = output.IsChecked == true, Visible = visible.IsChecked == true }; c.Apply(d with { Layers = layers }); }
        catch (Exception ex) { status.Text = ex.Message; }
    }
    private static FilePickerFileType ProjectType => new("LaserCraft project") { Patterns = ["*.lcraft"] };
    private async Task Open()
    {
        try { var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open LaserCraft project", AllowMultiple = false, FileTypeFilter = [ProjectType] }); if (files.Count > 0 && files[0].TryGetLocalPath() is string path) AddDocument(ProjectFile.Load(path), path); }
        catch (Exception ex) { status.Text = "Open failed: " + ex.Message; }
    }
    private async Task Save(bool saveAs)
    {
        try
        {
            var s = Current; var path = s.Path;
            if (saveAs || path is null) { var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Save LaserCraft project", SuggestedFileName = s.Canvas.History.Document.Name + ".lcraft", DefaultExtension = "lcraft", FileTypeChoices = [ProjectType] }); path = file?.TryGetLocalPath(); }
            if (path is null) return; ProjectFile.Save(path, s.Canvas.History.Document); s.Path = path; s.Saved = Snapshot(s); Refresh(); status.Text = "Saved " + path;
        }
        catch (Exception ex) { status.Text = "Save failed: " + ex.Message; }
    }
    private async Task Export()
    {
        try { var s = Current; var file = await StorageProvider.SaveFilePickerAsync(new() { Title = "Export geometry as SVG", SuggestedFileName = "design.svg", DefaultExtension = "svg", FileTypeChoices = [new("SVG") { Patterns = ["*.svg"] }] }); if (file?.TryGetLocalPath() is string path) { await File.WriteAllTextAsync(path, SvgExport.Write(s.Canvas.History.Document)); status.Text = "SVG exported. Cut settings remain in the LaserCraft project."; } }
        catch (Exception ex) { status.Text = "Export failed: " + ex.Message; }
    }
    private void ToggleTheme()
    {
        if (Avalonia.Application.Current is not { } app) return;
        app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark ? ThemeVariant.Light : ThemeVariant.Dark; ApplyPalette();
        try { Directory.CreateDirectory(StateFolder); File.WriteAllText(settingsPath, JsonSerializer.Serialize(new { Light = app.RequestedThemeVariant == ThemeVariant.Light })); } catch (IOException ex) { status.Text = "Theme could not be saved: " + ex.Message; }
    }
    private void LoadSettings()
    {
        try { if (File.Exists(settingsPath) && JsonDocument.Parse(File.ReadAllText(settingsPath)).RootElement.GetProperty("Light").GetBoolean()) Avalonia.Application.Current!.RequestedThemeVariant = ThemeVariant.Light; }
        catch (Exception ex) when (ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { status.Text = "Using default theme; saved settings could not be read."; }
        ApplyPalette();
    }
    private void ApplyPalette()
    {
        var light = Avalonia.Application.Current?.RequestedThemeVariant == ThemeVariant.Light;
        Background = new SolidColorBrush(Color.Parse(light ? "#EEF3F7" : "#111923"));
        Foreground = new SolidColorBrush(Color.Parse(light ? "#172A3C" : "#DEE8F3"));
        foreach (var label in accentLabels) label.Foreground = new SolidColorBrush(Color.Parse(light ? "#006D75" : "#40E0D0"));
    }
    private async void OnShortcut(object? sender, KeyEventArgs e)
    {
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if (e.Source is TextBox) return;
        if (e.Key == Key.A) { Current.Canvas.SelectAll(); e.Handled = true; }
        if (e.Key == Key.C) { Copy(); e.Handled = true; }
        if (e.Key == Key.V) { Paste(); e.Handled = true; }
        if (e.Key == Key.D) { Duplicate(); e.Handled = true; }
        if (e.Key == Key.S) { await Save(false); e.Handled = true; }
        if (e.Key == Key.Z) { Undo(); e.Handled = true; }
        if (e.Key == Key.Y) { Redo(); e.Handled = true; }
    }
}
