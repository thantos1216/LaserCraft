using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.Styling;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private readonly NumericUpDown passes = Number(1, 100), interval = Number(.01m, 100);
    private readonly ComboBox mode = new() { ItemsSource = new[] { "Line", "Fill" }, SelectedIndex = 0, MinWidth = 90 };
    private readonly CheckBox air = new() { Content = "Air assist", MinHeight = 24 }, output = new() { Content = "Output", MinHeight = 24 }, visible = new() { Content = "Show", MinHeight = 24 };
    private readonly ListBox layerRows = new() { MinHeight = 90 };
    private readonly WrapPanel palette = new() { Orientation = Orientation.Horizontal };
    private readonly TabControl upperPanels = new(), lowerPanels = new();
    private readonly TextBlock jobStatus = new() { Text = "Offline job simulator · no machine execution", FontSize = 12 }, estimate = new() { FontSize = 12 };
    private readonly ProgressBar jobProgress = new() { Minimum = 0, Maximum = 100, Height = 4, Margin = new Thickness(4, 8) };
    private readonly TextBox console = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Text = "LaserCraft simulator ready. No serial or network transport is active.", FontSize = 12 };
    private readonly CheckBox cutSelected = new() { Content = "Cut selected graphics", MinHeight = 24 }, snapControl = new() { Content = "Snap · 1 mm", IsChecked = true, MinHeight = 24 };
    private readonly NumericUpDown playbackSpeed = Number(1, 100);
    private JobSimulation? simulation;
    private Session? simulationOwner;
    private string simulationDocument = "";
    private string simulationScope = "";
    private string CurrentSimulationScope => cutSelected.IsChecked == true ? "selected:" + string.Join(',', Current.Canvas.Selection.Order()) : "all";
    private readonly DispatcherTimer simulationTimer = new() { Interval = TimeSpan.FromMilliseconds(50) };
    private DesignShape[] clipboard = [];
    private CutLayer[] clipboardLayers = [];
    private readonly Dictionary<string, Button> toolButtons = [];

    private void BuildWorkspace()
    {
        Title = "LaserCraft · Untitled"; Width = 1480; Height = 940; MinWidth = 1100; MinHeight = 720; FontFamily = new FontFamily("Segoe UI"); FontSize = 12; ApplyPalette();
        Styles.Add(new Style(s => s.OfType<TabItem>()) { Setters = { new Setter(TabItem.FontSizeProperty, 12d), new Setter(TabItem.PaddingProperty, new Thickness(7, 5)), new Setter(TabItem.MinHeightProperty, 28d) } });
        foreach (var check in new[] { air, output, visible, snapControl, cutSelected }) check.Margin = new Thickness(5, 0);
        interval.Increment = .01m;
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto,Auto"), Margin = new Thickness(5) };
        var menu = new Menu
        {
            ItemsSource = new[]{
            MenuGroup("File",Item("New",()=>AddDocument(DesignDocument.Empty())),AsyncItem("Open…",Open),AsyncItem("Import SVG…",ImportSvg),AsyncItem("Import image…",ImportImage),AsyncItem("Save",()=>Save(false)),AsyncItem("Save as…",()=>Save(true)),AsyncItem("Export SVG…",Export),Item("Close project",()=>_=CloseDocument()),Item("Exit",Close)),
            MenuGroup("Edit",Item("Undo",Undo),Item("Redo",Redo),Item("Copy",Copy),Item("Cut",()=>{ Copy(); Current.Canvas.DeleteSelected(); }),Item("Paste",Paste),Item("Duplicate",Duplicate),Item("Delete",()=>Current.Canvas.DeleteSelected()),Item("Select all",()=>Current.Canvas.SelectAll())),
            MenuGroup("Tools",Item("Select",()=>SetTool("Select")),Item("Rectangle",()=>SetTool("Rectangle")),Item("Ellipse",()=>SetTool("Ellipse")),Item("Draw lines",()=>SetTool("Lines")),Item("Edit nodes",()=>SetTool("Nodes")),Item("Insert node after selected",()=>EditPathNode("Insert")),Item("Delete selected node",()=>EditPathNode("Remove")),Item("Break at selected node",()=>EditPathNode("Break")),Item("Remove selected contour",()=>EditPathNode("Contour")),Item("Separate selected paths",()=>ComposePaths(true)),Item("Combine selected paths",()=>ComposePaths(false)),AsyncItem("Join open endpoints…",JoinPaths),Item("Convert selected to paths",ConvertToPaths),Item("Close selected paths",ClosePaths),Unavailable("Editable Bézier handles"),AsyncItem("Create text…",()=>EditText(false)),AsyncItem("Edit selected text…",()=>EditText(true)),AsyncItem("Trace image…",TraceImage)),
            MenuGroup("Arrange",Item("Flip horizontal",()=>Mirror(true)),Item("Flip vertical",()=>Mirror(false)),AsyncItem("Rotate vectors…",RotateVectors),MenuGroup("Align",new[]{"Left","Right","Top","Bottom","Horizontal center","Vertical center"}.Select(a=>Item(a,()=>Align(a))).ToArray()),MenuGroup("Distribute",Enum.GetValues<DistributionMode>().Select(m=>Item(m switch { DistributionMode.HorizontalCenters=>"Horizontal centers",DistributionMode.VerticalCenters=>"Vertical centers",DistributionMode.HorizontalGaps=>"Horizontal gaps",_=>"Vertical gaps" },()=>Distribute(m))).ToArray()),AsyncItem("Rectangular array…",RectangularArray),Item("Reverse path direction",ReversePaths),Item("Center on bed",CenterOnBed),Unavailable("Group / Ungroup"),Unavailable("Boolean operations"),Unavailable("Nesting")),
            MenuGroup("Laser Tools",Item("Preview",Preview),Item("Devices…",()=>_=EditDevices()),Unavailable("Material test"),Unavailable("Rotary setup"),Unavailable("Camera calibration"),Unavailable("Print and cut")),
            MenuGroup("Web Tools",Unavailable("Online integrations")),
            MenuGroup("Window",Item("Cuts / Layers",()=>upperPanels.SelectedIndex=0),Item("Console",()=>upperPanels.SelectedIndex=1),Item("Laser",()=>lowerPanels.SelectedIndex=0),Item("Help",ShowHelp),Item("Fit workspace",()=>Current.Canvas.Fit()),Item("Light / dark",ToggleTheme)),
            MenuGroup("Language",Unavailable("English (current)")),MenuGroup("Help",Item("LaserCraft Help",ShowHelp),Item("Feature status",ShowFeatureStatus)) }
        };
        root.Children.Add(menu);
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Margin = new Thickness(0, 3) };
        toolbar.Children.Add(IconButton("New", "M3,1 L11,1 L15,5 L15,17 L3,17 Z M11,1 L11,5 L15,5", () => AddDocument(DesignDocument.Empty())));
        toolbar.Children.Add(IconButton("Open", "M1,5 L7,5 L9,7 L17,7 L15,16 L1,16 Z", () => _ = Open()));
        toolbar.Children.Add(IconButton("Save", "M2,2 L14,2 L17,5 L17,17 L2,17 Z M5,2 L5,7 L13,7 L13,2 M5,17 L5,11 L14,11 L14,17", () => _ = Save(false)));
        toolbar.Children.Add(IconButton("Import SVG", "M2,1 L12,1 L16,5 L16,17 L2,17 Z M5,9 L12,9 M9,6 L12,9 L9,12", () => _ = ImportSvg()));
        Separator(toolbar); toolbar.Children.Add(IconButton("Undo", "M5,4 L1,8 L5,12 M1,8 L10,8 Q17,8 17,15", Undo)); toolbar.Children.Add(IconButton("Redo", "M13,4 L17,8 L13,12 M17,8 L8,8 Q1,8 1,15", Redo));
        Separator(toolbar); toolbar.Children.Add(IconButton("Copy", "M2,2 L12,2 L12,13 L2,13 Z M6,6 L16,6 L16,17 L6,17 Z", Copy)); toolbar.Children.Add(IconButton("Paste", "M5,3 L2,3 L2,17 L16,17 L16,3 L13,3 M6,1 L12,1 L12,5 L6,5 Z", Paste)); toolbar.Children.Add(IconButton("Delete", "M2,4 L16,4 M6,4 L6,1 L12,1 L12,4 M4,4 L5,17 L13,17 L14,4 M8,7 L8,14 M11,7 L11,14", () => Current.Canvas.DeleteSelected()));
        Separator(toolbar); toolbar.Children.Add(IconButton("Fit workspace", "M1,6 L1,1 L6,1 M12,1 L17,1 L17,6 M17,12 L17,17 L12,17 M6,17 L1,17 L1,12", () => Current.Canvas.Fit()));
        toolbar.Children.Add(IconButton("Preview", "M1,2 L17,2 L17,14 L1,14 Z M7,5 L12,8 L7,11 Z M6,17 L12,17", Preview));
        toolbar.Children.Add(Button("Light / dark", ToggleTheme)); toolbar.Children.Add(Label("LASERCRAFT  /  0.18", 12));
        Grid.SetRow(toolbar, 1); root.Children.Add(toolbar);
        var transforms = new WrapPanel { Margin = new Thickness(0, 2, 0, 5) };
        Inline(transforms, "X", x, 90); Inline(transforms, "Y", y, 90); Inline(transforms, "Width", width, 90); Inline(transforms, "Height", height, 90); transforms.Children.Add(Label("mm")); transforms.Children.Add(Button("Apply", ApplyGeometry)); transforms.Children.Add(Button("Center on bed", CenterOnBed)); transforms.Children.Add(snapControl); transforms.Children.Add(selection);
        snapControl.IsCheckedChanged += (_, _) => { if (!updatingInspector && sessions.Count > 0) Current.Canvas.Snap = snapControl.IsChecked == true; };
        Grid.SetRow(transforms, 2); root.Children.Add(transforms);
        var workspace = new Grid { ColumnDefinitions = new ColumnDefinitions("40,*,5,520") };
        var drawingTools = new StackPanel { Spacing = 3, Margin = new Thickness(1, 8) };
        AddTool("Select", "M3,1 L3,15 L7,11 L11,17 L14,15 L10,9 L16,8 Z");
        AddTool("Rectangle", "M2,2 L16,2 L16,16 L2,16 Z"); AddTool("Ellipse", "M9,1 A8,8 0 1 1 8.99,1 Z");
        drawingTools.Children.Add(IconButton("Duplicate", "M2,2 L12,2 L12,12 L2,12 Z M6,6 L16,6 L16,16 L6,16 Z", Duplicate));
        AddTool("Lines", "M2,15 L7,3 L16,12 M1,14 L3,14 L3,16 L1,16 Z M6,2 L8,2 L8,4 L6,4 Z M15,11 L17,11 L17,13 L15,13 Z");
        AddTool("Nodes", "M2,13 L8,4 L16,10 M1,11 L4,11 L4,14 L1,14 Z M6,2 L10,2 L10,6 L6,6 Z M14,8 L17,8 L17,11 L14,11 Z");
        var textTool = Button("T", () => _ = EditText(false)); ToolTip.SetTip(textTool, "Create text"); drawingTools.Children.Add(textTool);
        drawingTools.Children.Add(IconButton("Trace image", "M2,14 L5,4 L10,2 L16,6 L14,16 Z M4,14 L7,7 L11,5 L14,8", () => _ = TraceImage()));
        workspace.Children.Add(drawingTools); Grid.SetColumn(tabs, 1); workspace.Children.Add(tabs);
        var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns, HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetColumn(splitter, 2); workspace.Children.Add(splitter);
        var right = new Grid { RowDefinitions = new RowDefinitions("3*,5,2*"), Margin = new Thickness(5, 0, 0, 0) };
        upperPanels.Items.Add(new TabItem { Header = "Cuts / Layers", Content = BuildCutsPanel() }); upperPanels.Items.Add(new TabItem { Header = "Console", Content = console });
        upperPanels.Items.Add(new TabItem { Header = "Move", Content = BuildMovePanel() });
        foreach (var name in new[] { "Macros", "Cameras / Overlays", "Variable Text", "Pages" }) { var tab = new TabItem { Header = name, IsEnabled = false }; ToolTip.SetTip(tab, name + " is planned; no implementation in this build."); upperPanels.Items.Add(tab); }
        right.Children.Add(upperPanels); var hsplit = new GridSplitter { ResizeDirection = GridResizeDirection.Rows, VerticalAlignment = VerticalAlignment.Stretch, HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetRow(hsplit, 1); right.Children.Add(hsplit);
        lowerPanels.Items.Add(new TabItem { Header = "Laser", Content = BuildLaserPanel() }); lowerPanels.Items.Add(new TabItem { Header = "Help", Content = new ScrollViewer { Content = help, Margin = new Thickness(12) } });
        var materials = new TabItem { Header = "Material Library", IsEnabled = false }; ToolTip.SetTip(materials, "Material libraries are planned."); lowerPanels.Items.Add(materials); Grid.SetRow(lowerPanels, 2); right.Children.Add(lowerPanels);
        Grid.SetColumn(right, 3); workspace.Children.Add(right); Grid.SetRow(workspace, 3); root.Children.Add(workspace);
        palette.Margin = new Thickness(42, 5, 0, 2); Grid.SetRow(palette, 4); root.Children.Add(palette); status.Margin = new Thickness(5, 2); Grid.SetRow(status, 5); root.Children.Add(status); Content = root;
        layer.SelectionChanged += (_, _) => { if (!updatingInspector && sessions.Count > 0 && layer.SelectedIndex >= 0) { Current.Canvas.ActiveLayer = layer.SelectedIndex; QueueLayerRefresh(); } };
        layerRows.SelectionChanged += (_, _) => { if (!updatingInspector && layerRows.SelectedItem is ListBoxItem row && row.Tag is int i) { Current.Canvas.ActiveLayer = i; QueueLayerRefresh(); } };
        simulationTimer.Tick += (_, _) => { if (simulation is null) return; var now = playbackClock.Elapsed.TotalSeconds; simulation.Advance((now - lastPlaybackTick) * (double)(playbackSpeed.Value ?? 10)); lastPlaybackTick = now; var c = Current.Canvas; c.PreviewSeconds = simulation.Elapsed; c.InvalidateVisual(); UpdateSimulation(); if (simulation.State != SimulationState.Running) simulationTimer.Stop(); };
        Closed += (_, _) => { workspaceClosed = true; previewCancellation?.Cancel(); simulationTimer.Stop(); };
        cutSelected.IsCheckedChanged += (_, _) => { if (sessions.Count > 0) RefreshWorkspace(); };
        void AddTool(string name, string geometry) { var b = IconButton(name, geometry, () => SetTool(name)); toolButtons[name] = b; drawingTools.Children.Add(b); }
    }
    private Control BuildCutsPanel()
    {
        var panel = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(4) };
        var headers = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*,48,76,42,38,30"), Margin = new Thickness(8, 4) };
        var names = new[] { "#", "Layer", "Mode", "Spd/Pwr", "Out", "Show", "Air" }; for (int i = 0; i < names.Length; i++) { var t = new TextBlock { Text = names[i], FontSize = 11 }; Grid.SetColumn(t, i); headers.Children.Add(t); }
        panel.Children.Add(headers);
        Grid.SetRow(layerRows, 1); panel.Children.Add(layerRows);
        var settings = new StackPanel { Spacing = 3, Margin = new Thickness(3, 4) }; var layerActions = new WrapPanel(); layerActions.Children.Add(Button("+ Layer", () => _ = NewLayer())); layerActions.Children.Add(Button("↑", () => MoveLayer(-1))); layerActions.Children.Add(Button("↓", () => MoveLayer(1))); layerActions.Children.Add(Button("Delete empty", RemoveLayer)); layerActions.Children.Add(Button("Assign selected", () => AssignLayer(Current.Canvas.ActiveLayer))); layerActions.Children.Add(Button("Image settings…", () => _ = EditImageSettings())); settings.Children.Add(layerActions); settings.Children.Add(layer); var identity = new WrapPanel(); Inline(identity, "Layer name", layerName, 220); Inline(identity, "Layer color", layerColor, 110); layerColor.Click += (_, _) => _ = PickLayerColor(); settings.Children.Add(identity);
        var values = new WrapPanel(); Inline(values, "Speed mm/min", speed, 100); Inline(values, "Power %", power, 75); Inline(values, "Passes", passes, 65); Inline(values, "Interval mm", interval, 80); values.Children.Add(mode); settings.Children.Add(values);
        var switches = new WrapPanel(); switches.Children.Add(output); switches.Children.Add(visible); switches.Children.Add(air); switches.Children.Add(Button("Apply cut settings", ApplyLayer)); settings.Children.Add(switches);
        Grid.SetRow(settings, 2); panel.Children.Add(settings); return panel;
    }
    private Control BuildLaserPanel()
    {
        var panel = new StackPanel { Spacing = 5, Margin = new Thickness(8) }; panel.Children.Add(jobStatus);
        var controls = new UniformGrid { Columns = 3 }; controls.Children.Add(Button("Ⅱ Pause", PauseSimulation)); controls.Children.Add(Button("■ Stop", StopSimulation)); controls.Children.Add(Button("▶ Start", StartSimulation)); panel.Children.Add(controls);
        var actions = new WrapPanel(); actions.Children.Add(Button("Preview", Preview)); actions.Children.Add(Button("Frame preview", FramePreview));
        actions.Children.Add(AsyncButton("Save G-code", SaveGcode));
        foreach (var name in new[] { "Run G-code", "Home", "Go to origin" }) { var b = Button(name, () => { }); b.IsEnabled = false; ToolTip.SetTip(b, "Machine execution is not implemented yet."); actions.Children.Add(b); }
        panel.Children.Add(actions); panel.Children.Add(BuildConnectionPanel());
        var view = new WrapPanel(); playbackSpeed.Value = 10; Inline(view, "Playback speed ×", playbackSpeed, 85); var travel = new CheckBox { Content = "Show travel", IsChecked = true, Margin = new Thickness(8) }; travel.IsCheckedChanged += (_, _) => { Current.Canvas.ShowTravel = travel.IsChecked == true; Current.Canvas.InvalidateVisual(); }; view.Children.Add(travel); view.Children.Add(Button("Return to design", StopSimulation)); panel.Children.Add(view);
        panel.Children.Add(cutSelected); panel.Children.Add(estimate); panel.Children.Add(jobProgress); panel.Children.Add(Label("Preview shows power-weighted black marks and the moving beam on a white bed. Design coordinates: top-left origin. Ideal timing excludes acceleration, controller delays and material effects."));
        return new ScrollViewer { Content = panel };
    }
    private static void Inline(Panel p, string name, Control control, double width) { var column = new StackPanel { Margin = new Thickness(3, 0), Spacing = 2 }; column.Children.Add(new TextBlock { Text = name, FontSize = 11 }); control.Width = width; control.MinHeight = 26; column.Children.Add(control); p.Children.Add(column); }
    private bool layerRefreshQueued;
    private void QueueLayerRefresh()
    {
        if (layerRefreshQueued) return;
        layerRefreshQueued = true;
        // Replacing list items inside its selection transaction invalidates Avalonia's selected-item enumerator.
        Dispatcher.UIThread.Post(() => { layerRefreshQueued = false; if (!workspaceClosed && sessions.Count > 0) Refresh(); });
    }
    private static void Separator(Panel p) => p.Children.Add(new Border { Width = 1, Background = Brushes.SlateGray, Margin = new Thickness(5, 3) });
    private static MenuItem Item(string name, Action action) { var item = new MenuItem { Header = name }; item.Click += (_, _) => action(); return item; }
    private static MenuItem AsyncItem(string name, Func<Task> action) { var item = new MenuItem { Header = name }; item.Click += async (_, _) => await action(); return item; }
    private static MenuItem MenuGroup(string name, params MenuItem[] items) => new() { Header = name, ItemsSource = items };
    private static MenuItem Unavailable(string name) { var item = new MenuItem { Header = name, IsEnabled = false }; ToolTip.SetTip(item, "Not implemented yet. See Help / Feature status."); return item; }
    private static Button IconButton(string name, string data, Action action)
    {
        var b = Button("", action); b.Width = 32; b.Height = 30; b.Padding = new Thickness(6); b.Content = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse(data), Stroke = Brushes.SlateGray, StrokeThickness = 1.6, Width = 18, Height = 18, Stretch = Stretch.Uniform }; ToolTip.SetTip(b, name); AutomationProperties.SetName(b, name); return b;
    }
    private void SetTool(string tool) { var c = Current.Canvas; if (tool != "Select" && c.History.Document.Layers[c.ActiveLayer].Kind == LayerKind.Image) { var target = c.History.Document.Layers.FindIndex(l => l.Kind == LayerKind.Vector); if (target < 0) { var next = LayerEditing.Add(c.History.Document, "Drawing", LayerKind.Vector); c.ActiveLayer = next.Layers.Count - 1; c.Apply(next); } else c.ActiveLayer = target; } c.CancelDrawing(); c.Tool = tool; StopSimulation(); Refresh(); status.Text = tool == "Lines" ? "Click vertices · Enter/double-click finishes · C or first vertex closes · Escape cancels" : tool == "Nodes" ? "Click a path node to select it · drag to move · double-click an edge to insert · Delete removes a node · Tools has break/contour operations." : tool + " · drag to draw or move; Ctrl-click adds to selection"; }
    private void Undo() { StopSimulation(); Current.Canvas.History.Undo(); Current.Canvas.Refresh(); }
    private void Redo() { StopSimulation(); Current.Canvas.History.Redo(); Current.Canvas.Refresh(); }
    private void Copy() { clipboard = Current.Canvas.History.Document.Shapes.Where(s => Current.Canvas.Selection.Contains(s.Id)).ToArray(); clipboardLayers = Current.Canvas.History.Document.Layers.ToArray(); status.Text = $"Copied {clipboard.Length} objects"; }
    private void Paste()
    {
        if (clipboard.Length == 0) return; var c = Current.Canvas; var layers = c.History.Document.Layers.ToList();
        var added = clipboard.Select(s => { var source = clipboardLayers[s.Layer]; var target = s.Kind == ShapeKind.Image ? -1 : layers.FindIndex(l => l.Color == source.Color && l.Kind == LayerKind.Vector); if (target < 0) { target = layers.Count; layers.Add(source with { Name = source.Name + " copy" }); } return s with { Id = Guid.NewGuid(), X = s.X + 5, Y = s.Y + 5, Layer = target }; }).ToList();
        try { c.Apply(c.History.Document with { Shapes = [.. c.History.Document.Shapes, .. added], Layers = layers }); c.Selection.Clear(); foreach (var s in added) c.Selection.Add(s.Id); c.Refresh(); } catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
    private void Duplicate() { Copy(); Paste(); }
    private void Align(string alignment) { var c = Current.Canvas; if (c.Selection.Count > 1) c.Apply(ShapeEditing.Align(c.History.Document, c.Selection, alignment)); }
    private void Mirror(bool horizontal) { var c = Current.Canvas; if (c.Selection.Count > 0) c.Apply(ShapeEditing.Mirror(c.History.Document, c.Selection, horizontal)); }
    private void CenterOnBed()
    {
        var c = Current.Canvas; if (c.Selection.Count == 0) return; var d = c.History.Document; var b = ShapeEditing.Bounds(d.Shapes.Where(s => c.Selection.Contains(s.Id)));
        c.Apply(ShapeEditing.Transform(d, c.Selection, s => s with { X = s.X + (d.Width - b.Width) / 2 - b.X, Y = s.Y + (d.Height - b.Height) / 2 - b.Y }));
    }
    private void ShowHelp() => lowerPanels.SelectedIndex = 1;
    private void ShowFeatureStatus() { ShowHelp(); status.Text = "Implemented: shapes, layers, SVG subset, image adjustments/dithering, raster/vector simulation. Disabled commands are planned; hardware execution is unavailable."; }
    private CancellationTokenSource? previewCancellation;
    private Session? previewOwner;
    private string previewDocument = "", previewScope = "";
    private readonly SemaphoreSlim previewGate = new(1);
    private bool workspaceClosed;
    private readonly System.Diagnostics.Stopwatch playbackClock = new();
    private double lastPlaybackTick;
    private void Preview() => _ = GeneratePreview(false);
    private async Task GeneratePreview(bool startPlayback)
    {
        StopSimulation(); simulation = null; var session = Current; var c = session.Canvas; c.Plan = null;
        var document = c.History.Document; var selected = cutSelected.IsChecked == true ? c.Selection.ToHashSet() : null;
        var fingerprint = Snapshot(session); var scope = CurrentSimulationScope;
        using var cancel = new CancellationTokenSource(); previewCancellation = cancel; previewOwner = session; previewDocument = fingerprint; previewScope = scope;
        jobStatus.Text = "Preparing vector/raster preview…";
        try
        {
            await previewGate.WaitAsync(cancel.Token);
            ToolpathPlan plan;
            try { plan = await Task.Run(() => ToolpathPlanner.Build(document, selected, RasterImages.EngravingGrid, cancel.Token), cancel.Token); }
            finally { previewGate.Release(); }
            if (workspaceClosed || cancel.IsCancellationRequested || Current != session || Snapshot(session) != fingerprint || CurrentSimulationScope != scope) return;
            simulation = new(plan); simulationOwner = session; simulationDocument = fingerprint; simulationScope = scope;
            c.Plan = plan; c.ShowPreview = true; c.PreviewSeconds = double.PositiveInfinity; c.InvalidateVisual();
            estimate.Text = $"{plan.Segments.Count} segments · {plan.CutLength:0.0} mm cut · ideal {plan.Seconds:0.0} s";
            if (plan.Segments.Count > 25000) estimate.Text += " · dense drawing sampled";
            Log("Preview generated."); UpdateSimulation();
            if (startPlayback) { simulation.Start(); c.PreviewSeconds = 0; playbackClock.Restart(); lastPlaybackTick = 0; simulationTimer.Start(); UpdateSimulation(); }
        }
        catch (OperationCanceledException) { if (!workspaceClosed && previewCancellation == cancel) jobStatus.Text = "Preview cancelled"; }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { if (!workspaceClosed && previewCancellation == cancel) { jobStatus.Text = "Preview failed"; status.Text = ex.Message; } }
        finally { if (previewCancellation == cancel) previewCancellation = null; }
    }
    private void StartSimulation()
    {
        if (simulation?.State == SimulationState.Paused) { simulation.Start(); playbackClock.Restart(); lastPlaybackTick = 0; simulationTimer.Start(); UpdateSimulation(); }
        else _ = GeneratePreview(true);
    }
    private void PauseSimulation() { simulation?.Pause(); playbackClock.Stop(); simulationTimer.Stop(); UpdateSimulation(); }
    private void StopSimulation() { previewCancellation?.Cancel(); playbackClock.Reset(); lastPlaybackTick = 0; simulationTimer.Stop(); simulation?.Stop(); if (sessions.Count > 0) { Current.Canvas.ShowPreview = false; Current.Canvas.InvalidateVisual(); } UpdateSimulation(); }
    private void UpdateSimulation() { jobStatus.Text = "SIMULATOR · " + (simulation?.State.ToString() ?? "Ready") + " · no machine execution"; jobProgress.Value = simulation is null || simulation.Plan.Seconds == 0 ? 0 : simulation.Elapsed / simulation.Plan.Seconds * 100; }
    private async void FramePreview() { await GeneratePreview(false); if (Current.Canvas.Plan is { } p && p.Segments.Count > 0) { Current.Canvas.Fit(); status.Text = "Output bounds checked against the bed. Frame preview only; no machine motion."; } }
    private void Log(string text) { console.Text += $"\n{DateTime.Now:HH:mm:ss}  {text}"; }
    private void RefreshWorkspace()
    {
        if (sessions.Count == 0) return; var c = Current.Canvas; var d = c.History.Document;
        if (previewCancellation is not null && (previewOwner != Current || previewDocument != Snapshot(Current) || previewScope != CurrentSimulationScope)) previewCancellation.Cancel();
        if (simulation is not null && (simulationOwner != Current || simulationDocument != Snapshot(Current) || simulationScope != CurrentSimulationScope)) { StopSimulation(); if (simulationOwner is not null) { simulationOwner.Canvas.ShowPreview = false; simulationOwner.Canvas.Plan = null; } simulation = null; estimate.Text = "Design or selection changed · regenerate preview"; }
        Title = $"{d.Name}{(IsDirty(Current) ? " *" : "")} · LaserCraft"; selection.Text = c.Selection.Count == 0 ? "No selection" : $"{c.Selection.Count} selected{c.NodeDescription}";
        updatingInspector = true; snapControl.IsChecked = c.Snap; layerRows.Items.Clear(); palette.Children.Clear();
        var used = Enumerable.Range(0, d.Layers.Count).ToArray();
        foreach (var i in used)
        {
            var l = d.Layers[i]; var row = new Grid { ColumnDefinitions = new ColumnDefinitions("32,*,48,76,42,38,30"), Margin = new Thickness(0, 3) };
            var cells = new[] { i.ToString("00"), l.Name, l.Mode.ToString(), $"{l.Speed:0}/{l.Power:0}", l.Output ? "●" : "○", l.Visible ? "●" : "○", l.AirAssist ? "●" : "○" };
            for (int col = 0; col < cells.Length; col++) { var t = new TextBlock { Text = cells[col], FontSize = 11, VerticalAlignment = VerticalAlignment.Center }; if (col == 0) t.Foreground = new SolidColorBrush(Color.Parse(l.Color)); Grid.SetColumn(t, col); row.Children.Add(t); }
            var item = new ListBoxItem { Content = row, Tag = i, Padding = new Thickness(4), MinHeight = 26 }; ToolTip.SetTip(item, "Select to edit this layer below"); layerRows.Items.Add(item); if (i == c.ActiveLayer) layerRows.SelectedItem = item;
        }
        for (int i = 0; i < d.Layers.Count; i++)
        {
            var index = i; var l = d.Layers[i]; var b = Button(i.ToString("00"), () => AssignLayer(index)); b.MinHeight = 25; b.Height = 25; b.MinWidth = 25; b.Width = 25; b.Padding = default; b.FontSize = 10; b.Background = new SolidColorBrush(Color.Parse(l.Color));
            var color = Color.Parse(l.Color); b.Foreground = color.R * .299 + color.G * .587 + color.B * .114 > 155 ? Brushes.Black : Brushes.White;
            b.BorderThickness = new Thickness(i == c.ActiveLayer ? 2 : 0); b.BorderBrush = Brushes.Turquoise; ToolTip.SetTip(b, $"{l.Name}: assign selection / choose drawing layer"); palette.Children.Add(b);
        }
        foreach (var pair in toolButtons) pair.Value.BorderBrush = pair.Key == c.Tool ? Brushes.Turquoise : Brushes.Transparent;
        updatingInspector = false;
    }
    private void AssignLayer(int index)
    {
        var c = Current.Canvas; c.ActiveLayer = index; if (c.Selection.Count > 0) { try { c.Apply(LayerEditing.Assign(c.History.Document, c.Selection, index)); } catch (InvalidDataException ex) { status.Text = ex.Message; } } else Refresh();
    }
    private async Task CloseDocument()
    {
        var s = Current; if (IsDirty(s)) { await Save(false); if (IsDirty(s)) return; }
        StopSimulation(); s.Canvas.ReleaseImages(); var index = tabs.SelectedIndex; sessions.Remove(s); tabs.Items.Remove(s.Tab); if (sessions.Count == 0) AddDocument(DesignDocument.Empty()); else { tabs.SelectedIndex = Math.Min(index, sessions.Count - 1); Refresh(); }
    }
}
