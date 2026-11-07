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
public sealed class MainWindow : Window
{
    private readonly TabControl tabs = new();
    private readonly List<Session> sessions = [];
    private readonly TextBlock status = new(), selection = new();
    private readonly ComboBox layer = new();
    private bool updatingInspector;
    private readonly List<TextBlock> accentLabels = [];
    private readonly NumericUpDown x = Number(), y = Number(), width = Number(.1m), height = Number(.1m), speed = Number(1, 100000), power = Number(0, 100);
    private readonly StackPanel help = new() { Spacing=8 };
    private static readonly string StateFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LaserCraft");
    private readonly string settingsPath = Path.Combine(StateFolder, "settings.json");
    private Session Current => sessions[tabs.SelectedIndex < 0 ? 0 : tabs.SelectedIndex];
    private sealed class Session { public DesignCanvas Canvas { get; } = new(); public string? Path { get; set; } public string Saved { get; set; } = ""; public TabItem Tab { get; } = new(); }
    private static NumericUpDown Number(decimal min = -10000, decimal max = 10000) => new() { Minimum=min, Maximum=max, Increment=1, FormatString="0.###", Value=min>0?min:0, HorizontalAlignment=HorizontalAlignment.Stretch };
    private static TextBlock Label(string text, double size=12) => new() { Text=text, FontSize=size, Margin=new Thickness(0,8,0,5), TextWrapping=TextWrapping.Wrap };
    private static Button Button(string label, Action action) { var b=new Button { Content=label, Margin=new Thickness(0,0,6,6), Padding=new Thickness(12,8) }; b.Click+=(_,_)=>action(); return b; }
    private static Button AsyncButton(string label, Func<Task> action) { var b=Button(label,()=>{}); b.Click+=async (_,_)=>await action(); return b; }
    public MainWindow()
    {
        Title="LaserCraft • Design studio"; Width=1440; Height=900; MinWidth=1060; MinHeight=680;
        FontFamily=new FontFamily("Segoe UI"); ApplyPalette();
        var root=new Grid { RowDefinitions=new RowDefinitions("Auto,Auto,*,Auto"), Margin=new Thickness(16) };
        var header=new Grid { ColumnDefinitions=new ColumnDefinitions("*,Auto") };
        var brand=new StackPanel(); var wordmark=new TextBlock { Text="LASERCRAFT",FontSize=27,FontWeight=FontWeight.Bold }; accentLabels.Add(wordmark); brand.Children.Add(wordmark); brand.Children.Add(Label("DESIGN STUDIO   /   FOUNDATION 0.1")); header.Children.Add(brand);
        var badge=Label("●  OFFLINE WORKSPACE  ·  No device connected",13); Grid.SetColumn(badge,1); header.Children.Add(badge); root.Children.Add(header);
        var toolbar=new WrapPanel { Margin=new Thickness(0,14,0,8) };
        toolbar.Children.Add(Button("New",()=>AddDocument(DesignDocument.Empty())));
        toolbar.Children.Add(AsyncButton("Open…",Open)); toolbar.Children.Add(AsyncButton("Save",()=>Save(false))); toolbar.Children.Add(AsyncButton("Save as…",()=>Save(true))); toolbar.Children.Add(AsyncButton("Export SVG…",Export));
        toolbar.Children.Add(Button("Undo",()=>{ Current.Canvas.History.Undo(); Current.Canvas.Refresh(); }));
        toolbar.Children.Add(Button("Redo",()=>{ Current.Canvas.History.Redo(); Current.Canvas.Refresh(); }));
        toolbar.Children.Add(Button("Fit bed",()=>Current.Canvas.Fit())); toolbar.Children.Add(Button("Light / dark",ToggleTheme));
        Grid.SetRow(toolbar,1); root.Children.Add(toolbar);
        var workspace=new Grid { ColumnDefinitions=new ColumnDefinitions("150,*,6,300") };
        var tools=new StackPanel { Margin=new Thickness(0,0,12,0), Spacing=5 };
        tools.Children.Add(Label("CREATE",14));
        foreach (var tool in new[]{"Select","Rectangle","Ellipse"}) tools.Children.Add(Button(tool,()=>{ Current.Canvas.Tool=tool; status.Text=$"{tool} tool · drag on the bed"; }));
        tools.Children.Add(Button("Delete",()=>Current.Canvas.DeleteSelected()));
        var snap=new CheckBox { Content="Snap to 1 mm", IsChecked=true, Margin=new Thickness(0,16,0,0) }; snap.IsCheckedChanged+=(_,_)=>Current.Canvas.Snap=snap.IsChecked==true; tools.Children.Add(snap);
        tools.Children.Add(Label("NAVIGATE",14)); tools.Children.Add(Label("Wheel to zoom\nMiddle-drag to pan\nFit bed to reset\n\nCtrl+S  Save\nCtrl+Z  Undo\nCtrl+Y  Redo\nDelete  Remove selection\nEsc  Deselect"));
        workspace.Children.Add(tools); Grid.SetColumn(tabs,1); workspace.Children.Add(tabs);
        var splitter=new GridSplitter { ResizeDirection=GridResizeDirection.Columns, HorizontalAlignment=HorizontalAlignment.Stretch }; Grid.SetColumn(splitter,2); workspace.Children.Add(splitter);
        var inspector=new StackPanel { Margin=new Thickness(16,0,0,0), Spacing=3 };
        inspector.Children.Add(Label("OBJECT PROPERTIES",14)); inspector.Children.Add(selection);
        AddField("X position · mm",x); AddField("Y position · mm",y); AddField("Width · mm",width); AddField("Height · mm",height);
        inspector.Children.Add(Button("Apply geometry",ApplyGeometry)); inspector.Children.Add(Label("CUT LAYERS",14)); inspector.Children.Add(layer);
        layer.SelectionChanged+=(_,_)=>{ if(!updatingInspector && sessions.Count>0 && layer.SelectedIndex>=0) { Current.Canvas.ActiveLayer=layer.SelectedIndex; RefreshLayer(); } };
        AddField("Speed · mm/min",speed); AddField("Power · %",power); inspector.Children.Add(Button("Apply layer settings",ApplyLayer));
        inspector.Children.Add(Label("Settings are stored for future job planning. Hardware control is not available in this build."));
        var sideTabs=new TabControl { ItemsSource=new[]{new TabItem { Header="Inspect", Content=new ScrollViewer { Content=inspector } },new TabItem { Header="Help", Content=new ScrollViewer { Content=help, Margin=new Thickness(16) } }} };
        Grid.SetColumn(sideTabs,3); workspace.Children.Add(sideTabs); Grid.SetRow(workspace,2); root.Children.Add(workspace);
        status.Margin=new Thickness(0,12,0,0); Grid.SetRow(status,3); root.Children.Add(status); Content=root;
        tabs.SelectionChanged+=(_,_)=>{ if(sessions.Count>0) Refresh(); };
        AddDocument(DesignDocument.Empty());
        var helpPath=Path.Combine(AppContext.BaseDirectory,"Help.md");
        foreach(var paragraph in (File.Exists(helpPath)?File.ReadAllText(helpPath):"Help document was not included in this build.").Replace("\r", "").Split("\n\n"))
        {
            var heading=paragraph.StartsWith('#'); var block=Label(paragraph.TrimStart('#',' '), heading?18:13);
            if(heading) { block.FontWeight=FontWeight.SemiBold; accentLabels.Add(block); } help.Children.Add(block);
        }
        LoadSettings(); KeyDown+=OnShortcut;
        Closing+=(_,e)=>{ if(!allowClose && sessions.Any(IsDirty)) { e.Cancel=true; _=ConfirmClose(); } };
        void AddField(string name, Control field) { inspector.Children.Add(Label(name)); inspector.Children.Add(field); }
    }
    private bool allowClose;
    private async Task ConfirmClose()
    {
        if(allowClose) return;
        var dialog=new Window { Title="Unsaved projects",Width=420,Height=180,WindowStartupLocation=WindowStartupLocation.CenterOwner,CanResize=false };
        var panel=new StackPanel { Margin=new Thickness(24) }; panel.Children.Add(Label("There are unsaved projects. Close and discard changes?",15));
        var buttons=new WrapPanel(); buttons.Children.Add(Button("Keep editing",()=>dialog.Close(false))); buttons.Children.Add(Button("Discard and close",()=>dialog.Close(true))); panel.Children.Add(buttons); dialog.Content=panel;
        if(await dialog.ShowDialog<bool>(this)) { allowClose=true; Close(); }
    }
    private static string Snapshot(Session s)=>JsonSerializer.Serialize(s.Canvas.History.Document);
    private static bool IsDirty(Session s)=>Snapshot(s)!=s.Saved;
    private void AddDocument(DesignDocument d,string? path=null)
    {
        var s=new Session { Path=path }; s.Canvas.History.Reset(d); s.Saved=Snapshot(s); s.Tab.Content=s.Canvas;
        s.Canvas.Changed+=Refresh; sessions.Add(s); tabs.Items.Add(s.Tab); tabs.SelectedIndex=sessions.Count-1; Refresh();
    }
    private void Refresh()
    {
        foreach(var s in sessions) s.Tab.Header=s.Canvas.History.Document.Name+(IsDirty(s)?" •":"");
        var c=Current.Canvas; var d=c.History.Document; var shape=d.Shapes.FirstOrDefault(s=>s.Id==c.Selected);
        updatingInspector=true; c.ActiveLayer=Math.Clamp(c.ActiveLayer,0,d.Layers.Count-1); layer.ItemsSource=d.Layers.Select((l,i)=>$"{i+1:00}  {l.Name}").ToArray(); layer.SelectedIndex=c.ActiveLayer; updatingInspector=false;
        selection.Text=shape is null?"Select an object to edit its dimensions.":$"{shape.Kind} · layer {shape.Layer+1}";
        if(shape is not null) { x.Value=(decimal)shape.X; y.Value=(decimal)shape.Y; width.Value=(decimal)shape.Width; height.Value=(decimal)shape.Height; }
        status.Text=$"{d.Name}  ·  {d.Width:0} × {d.Height:0} mm  ·  {d.Shapes.Count} objects  ·  {c.Tool}  ·  Offline"; RefreshLayer();
    }
    private void RefreshLayer() { var i=Math.Max(0,layer.SelectedIndex); var l=Current.Canvas.History.Document.Layers[i]; speed.Value=(decimal)l.Speed; power.Value=(decimal)l.Power; }
    private void ApplyGeometry()
    {
        try { var c=Current.Canvas; var d=c.History.Document; c.Apply(d with { Shapes=d.Shapes.Select(s=>s.Id==c.Selected?s with { X=(double)(x.Value??0),Y=(double)(y.Value??0),Width=(double)(width.Value??1),Height=(double)(height.Value??1),Layer=Math.Max(0,layer.SelectedIndex) }:s).ToList() }); }
        catch(Exception ex) { status.Text=ex.Message; }
    }
    private void ApplyLayer()
    {
        try { var c=Current.Canvas; var d=c.History.Document; var i=Math.Max(0,layer.SelectedIndex); var layers=d.Layers.ToList(); layers[i]=layers[i] with { Speed=(double)(speed.Value??1200),Power=(double)(power.Value??20) }; c.Apply(d with { Layers=layers }); }
        catch(Exception ex) { status.Text=ex.Message; }
    }
    private static FilePickerFileType ProjectType => new("LaserCraft project") { Patterns=["*.lcraft"] };
    private async Task Open()
    {
        try { var files=await StorageProvider.OpenFilePickerAsync(new() { Title="Open LaserCraft project",AllowMultiple=false,FileTypeFilter=[ProjectType] }); if(files.Count>0 && files[0].TryGetLocalPath() is string path) AddDocument(ProjectFile.Load(path),path); }
        catch(Exception ex) { status.Text="Open failed: "+ex.Message; }
    }
    private async Task Save(bool saveAs)
    {
        try {
            var s=Current; var path=s.Path;
            if(saveAs || path is null) { var file=await StorageProvider.SaveFilePickerAsync(new() { Title="Save LaserCraft project",SuggestedFileName=s.Canvas.History.Document.Name+".lcraft",DefaultExtension="lcraft",FileTypeChoices=[ProjectType] }); path=file?.TryGetLocalPath(); }
            if(path is null) return; ProjectFile.Save(path,s.Canvas.History.Document); s.Path=path; s.Saved=Snapshot(s); Refresh(); status.Text="Saved "+path;
        } catch(Exception ex) { status.Text="Save failed: "+ex.Message; }
    }
    private async Task Export()
    {
        try { var s=Current; var file=await StorageProvider.SaveFilePickerAsync(new() { Title="Export geometry as SVG",SuggestedFileName="design.svg",DefaultExtension="svg",FileTypeChoices=[new("SVG") { Patterns=["*.svg"] }] }); if(file?.TryGetLocalPath() is string path) { await File.WriteAllTextAsync(path,SvgExport.Write(s.Canvas.History.Document)); status.Text="SVG exported. Cut settings remain in the LaserCraft project."; } }
        catch(Exception ex) { status.Text="Export failed: "+ex.Message; }
    }
    private void ToggleTheme()
    {
        if(Avalonia.Application.Current is not { } app) return;
        app.RequestedThemeVariant=app.ActualThemeVariant==ThemeVariant.Dark?ThemeVariant.Light:ThemeVariant.Dark; ApplyPalette();
        try { Directory.CreateDirectory(StateFolder); File.WriteAllText(settingsPath,JsonSerializer.Serialize(new { Light=app.RequestedThemeVariant==ThemeVariant.Light })); } catch(IOException ex) { status.Text="Theme could not be saved: "+ex.Message; }
    }
    private void LoadSettings()
    {
        try { if(File.Exists(settingsPath) && JsonDocument.Parse(File.ReadAllText(settingsPath)).RootElement.GetProperty("Light").GetBoolean()) Avalonia.Application.Current!.RequestedThemeVariant=ThemeVariant.Light; }
        catch(Exception ex) when(ex is IOException or JsonException or KeyNotFoundException or InvalidOperationException) { status.Text="Using default theme; saved settings could not be read."; }
        ApplyPalette();
    }
    private void ApplyPalette()
    {
        var light=Avalonia.Application.Current?.RequestedThemeVariant==ThemeVariant.Light;
        Background=new SolidColorBrush(Color.Parse(light?"#EEF3F7":"#111923"));
        Foreground=new SolidColorBrush(Color.Parse(light?"#172A3C":"#DEE8F3"));
        foreach(var label in accentLabels) label.Foreground=new SolidColorBrush(Color.Parse(light?"#006D75":"#40E0D0"));
    }
    private async void OnShortcut(object? sender,KeyEventArgs e)
    {
        if(!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        if(e.Key==Key.S) { await Save(false); e.Handled=true; }
        if(e.Key==Key.Z && e.Source is not TextBox) { Current.Canvas.History.Undo(); Current.Canvas.Refresh(); e.Handled=true; }
        if(e.Key==Key.Y && e.Source is not TextBox) { Current.Canvas.History.Redo(); Current.Canvas.Refresh(); e.Handled=true; }
    }
}
