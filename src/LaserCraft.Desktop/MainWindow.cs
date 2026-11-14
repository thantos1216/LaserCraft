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
    private List<int> layerIndices = [];
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
        Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://LaserCraft.Desktop/Assets/LaserCraft.ico")));
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
        LoadSettings();InstallLayerBindings();InstallGeometryBindings(); KeyDown += OnShortcut;
        Closing += (_, e) => { if(allowClose)return;if(KeepPendingGeometry())CommitGeometryEdits();e.Cancel=true;if(shutdownInProgress)return;if(sessions.Any(IsDirty)){_=ConfirmClose();return;}_=ShutdownAndClose(); };
    }
    private bool shutdownInProgress;
    private async Task ShutdownAndClose()
    {
        if(shutdownInProgress)return;shutdownInProgress=true;explicitlyAbortedJob=null;SavePreferences();
        try{if(connectionAttempt is not null)await connectionAttempt.CancelAsync();await connectionLifetime.CancelAsync();if(connectionOperation is not null)await connectionOperation;await DisconnectDevice();}
        catch(Exception ex)when(ex is IOException or OperationCanceledException or ObjectDisposedException){connectionJournal?.Add("ERROR","Shutdown: "+ex.Message);}
        finally{allowClose=true;Close();}
    }
    private bool allowClose;
    private async Task ConfirmClose()
    {
        if (allowClose) return;
        var dialog = new Window { Title = "Unsaved projects", Width = 420, Height = 180, WindowStartupLocation = WindowStartupLocation.CenterOwner, CanResize = false };
        var panel = new StackPanel { Margin = new Thickness(24) }; panel.Children.Add(Label("There are unsaved projects. Close and discard changes?", 15));
        var buttons = new WrapPanel(); buttons.Children.Add(Button("Keep editing", () => dialog.Close(false))); buttons.Children.Add(Button("Discard and close", () => dialog.Close(true))); panel.Children.Add(buttons); dialog.Content = panel;
        if (await dialog.ShowDialog<bool>(this)) await ShutdownAndClose();
    }
    private static string Snapshot(Session s) => JsonSerializer.Serialize(s.Canvas.History.Document);
    private static bool IsDirty(Session s) => Snapshot(s) != s.Saved;
    private void AddDocument(DesignDocument d, string? path = null)
    {
        var device=connectedProfile??profiles.ElementAtOrDefault(deviceChoice.SelectedIndex);if(device is not null && device.Connection!=ConnectionKind.Simulator)d=d with {Width=device.BedWidth,Height=device.BedHeight};
        d.Validate(); RasterImages.Verify(d);
        var s = new Session { Path = path };s.Canvas.Inches=inches; s.Canvas.History.Reset(d); s.Canvas.ActiveLayer=LayerEditing.VisibleIndices(d).FirstOrDefault(); s.Saved = Snapshot(s); s.Tab.Content = s.Canvas;
        InstallCanvasMenu(s);
        s.Canvas.EditTextRequested += ()=>_ = EditText(true); s.Canvas.Changed += Refresh; s.Canvas.Notice += message => status.Text = message; sessions.Add(s); tabs.Items.Add(s.Tab); tabs.SelectedIndex = sessions.Count - 1; Refresh();
    }
    private void Refresh()
    {
        foreach (var s in sessions) s.Tab.Header = s.Canvas.History.Document.Name + (IsDirty(s) ? " •" : "");
        var c = Current.Canvas; var d = c.History.Document; var shape = d.Shapes.FirstOrDefault(s => s.Id == c.Selected);
        var selectedShapes=d.Shapes.Where(s=>c.Selection.Contains(s.Id)).ToArray();
        updatingInspector = true; c.ActiveLayer = Math.Clamp(c.ActiveLayer, 0, d.Layers.Count - 1); layerIndices=LayerEditing.VisibleIndices(d).ToList(); layer.ItemsSource = layerIndices.Select(i => $"{i:00}  {d.Layers[i].Name}").ToArray(); layer.SelectedIndex = layerIndices.IndexOf(c.ActiveLayer); updatingInspector = false;
        selection.Text = shape is null ? "Select an object to edit its dimensions." : $"{shape.Kind} · layer {shape.Layer + 1}";
        if (selectedShapes.Length > 0 && !KeepPendingGeometry()) { syncingGeometry=true; var bounds = ShapeEditing.Bounds(selectedShapes); SetMillimetres(x,bounds.X); SetMillimetres(y,bounds.Y); SetMillimetres(width,bounds.Width); SetMillimetres(height,bounds.Height); syncingGeometry=false; }
        RefreshWorkspace();
        RefreshStatus(); RefreshLayer();
    }
    private void RefreshStatus()
    {
        var c = Current.Canvas; var d = c.History.Document;
        status.Text = $"{d.Name}  ·  {DisplayValue(d.Width):0.###} × {DisplayValue(d.Height):0.###} {(inches?"in":"mm")}  ·  {d.Shapes.Count} objects  ·  {c.Tool}  ·  {(monitor is null ? connecting ? "Connecting" : "Disconnected" : PhysicalControlsAvailable ? "Physical control enabled" : manualConsole.IsChecked == true ? "Manual console enabled" : "Query connection")}";
    }
    private void RefreshLayer() { if(applyingLayerEdits)return;refreshingLayerEditor=true;try{ var i = Current.Canvas.ActiveLayer; var l = Current.Canvas.History.Document.Layers[i]; SetMillimetres(speed,l.Speed); power.Value = (decimal)l.Power; passes.Value = l.Passes; SetMillimetres(interval,l.Interval); mode.ItemsSource = l.Kind == LayerKind.Image ? new[] { "Image" } : new[] { "Line", "Fill" }; mode.SelectedIndex = l.Kind == LayerKind.Image ? 0 : (int)l.Mode; layerName.Text = l.Name; layerColorValue = l.Color; layerColor.Background = new SolidColorBrush(Color.Parse(l.Color)); air.IsChecked = l.AirAssist; output.IsChecked = l.Output; visible.IsChecked = l.Visible;passOrder.SelectedIndex=(int)l.PassOrder; }finally{refreshingLayerEditor=false;} }
    private void ApplyGeometry()=>CommitGeometryEdits();
    private bool CommitGeometryEdits()
    {
        geometryTimer?.Stop();geometryPending=false;
        try { var c=Current.Canvas;if(c.Selection.Count==0)return true;if(width.Value is null||height.Value is null||x.Value is null||y.Value is null)throw new InvalidDataException("Complete all dimensions first.");c.Apply(GeometrySizing.Resize(c.History.Document,c.Selection,Millimetres(x),Millimetres(y),Millimetres(width),Millimetres(height)));return true; }
        catch(InvalidDataException ex){status.Text="Size: "+ex.Message;return false;}
    }

    private static FilePickerFileType ProjectType => new("LaserCraft project") { Patterns = ["*.lcraft"] };
    private async Task Open()
    {
        try { var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Open LaserCraft project", AllowMultiple = false, FileTypeFilter = [ProjectType] }); if (files.Count > 0 && files[0].TryGetLocalPath() is string path) AddDocument(ProjectFile.Load(path), path); }
        catch (Exception ex) { status.Text = "Open failed: " + ex.Message; }
    }
    private async Task Save(bool saveAs)
    {
        if(!CommitLayerEdits())return;
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
        SavePreferences();
    }
    private void LoadSettings()
    {
        RestorePreferences();
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
        if (e.Key == Key.G) { ChangeGrouping(e.KeyModifiers.HasFlag(KeyModifiers.Shift)); e.Handled = true; }
        if (e.Key == Key.S) { await Save(false); e.Handled = true; }
        if (e.Key == Key.Z) { Undo(); e.Handled = true; }
        if (e.Key == Key.Y) { Redo(); e.Handled = true; }
    }
}
