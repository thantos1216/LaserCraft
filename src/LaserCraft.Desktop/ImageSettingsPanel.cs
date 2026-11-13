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
        var session = Current; var c = session.Canvas; var d = c.History.Document; var fingerprint = Snapshot(session);
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
        Control SliderInput(string name,NumericUpDown number,double min,double max,double step)
        {
            var panel=new StackPanel {Width=175,Margin=new Thickness(4)};Inline(panel,name,number,165);
            var slider=new Slider {Minimum=min,Maximum=max,Value=(double)(number.Value??0),SmallChange=step,LargeChange=step*10,IsSnapToTickEnabled=true,TickFrequency=step};
            Avalonia.Automation.AutomationProperties.SetName(slider,name);bool syncing=false;
            slider.ValueChanged+=(_,_)=>{if(syncing)return;syncing=true;number.Value=(decimal)(Math.Round(slider.Value/step)*step);syncing=false;};
            number.ValueChanged+=(_,_)=>{if(syncing)return;syncing=true;slider.Value=(double)(number.Value??0);syncing=false;};panel.Children.Add(slider);return panel;
        }
        var controls = new WrapPanel { Margin = new Thickness(0, 8) }; controls.Children.Add(imageMode); controls.Children.Add(SliderInput("Brightness %",brightness,-100,100,1)); controls.Children.Add(SliderInput("Contrast %",contrast,-100,100,1)); controls.Children.Add(SliderInput("Gamma",gamma,.1,5,.1)); var thresholdControl=SliderInput("Threshold",threshold,0,255,1);controls.Children.Add(thresholdControl); thresholdControl.IsEnabled=originalOptions.Mode==RasterMode.Threshold;imageMode.SelectionChanged+=(_,_)=>thresholdControl.IsEnabled=imageMode.SelectedItem is RasterMode.Threshold; Inline(controls, "Interval mm", spacing, 90); Inline(controls, "Overscan mm", overscan, 95); controls.Children.Add(invert); controls.Children.Add(bidirectional); Grid.SetRow(controls, 2); root.Children.Add(controls);
        Grid.SetRow(info, 3); root.Children.Add(info); var buttons = new WrapPanel(); Grid.SetRow(buttons, 4); root.Children.Add(buttons);
        using var lifetime = new CancellationTokenSource(); using var gate = new SemaphoreSlim(1);
        CancellationTokenSource? running = null; bool closed = false, resetting = false;
        var debounce = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        var jobs = new List<Task>(); ImagePreparationSettings? ready = null;
        double Required(NumericUpDown input)
        {
            if(input.Value is not decimal value||!decimal.TryParse(input.Text,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.CurrentCulture,out var typed)||typed<input.Minimum||typed>input.Maximum)throw new InvalidDataException("Fill in every numeric value within its range.");
            return (double)value;
        }
        ImagePreparationSettings Read()
        {
            var limit=Required(threshold); if(limit!=Math.Truncate(limit))throw new InvalidDataException("Threshold must be a whole number.");
            var options=new RasterSettings((RasterMode)(imageMode.SelectedItem ?? throw new InvalidDataException("Choose an image mode.")),Required(brightness),Required(contrast),Required(gamma),invert.IsChecked==true,(int)limit,bidirectional.IsChecked==true,Required(overscan));
            var settings=new ImagePreparationSettings(options,Required(spacing));settings.Validate();return settings;
        }
        var apply=Button("Apply",()=>
        {
            try
            {
                var settings=Read(); if(ready!=settings || running is not null)throw new InvalidDataException("Wait for the current preview to finish.");
                if(!sessions.Contains(session)||Current!=session||Snapshot(session)!=fingerprint)throw new InvalidDataException("The source document changed; reopen image preparation.");
                var next=settings.Apply(session.Canvas.History.Document,shape.Id);session.Canvas.CancelDrawing();session.Canvas.Apply(next);dialog.Close();
            }
            catch(InvalidDataException ex){info.Text=ex.Message;}
        });apply.IsEnabled=false;
        async Task Update()
        {
            using var request=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);running=request;var token=request.Token;Bitmap? next=null;
            try
            {
                var settings=Read();info.Text="Preparing current settings…";await gate.WaitAsync(token);
                RasterGrid grid;
                try { grid=await Task.Run(()=>RasterImages.EngravingGrid(shape,cut with {Interval=settings.Interval,Raster=settings.Raster},token),token);token.ThrowIfCancellationRequested();next=await Task.Run(()=>RasterImages.ProcessedPreview(grid),token); }
                finally { gate.Release(); }
                if(closed||token.IsCancellationRequested||running!=request||Read()!=settings)return;
                prepared.Source=next;processed?.Dispose();processed=next;next=null;ready=settings;apply.IsEnabled=true;
                info.Text=$"{grid.Width} × {grid.Height} samples · effective {grid.Width/shape.Width*25.4:0.0} × {grid.Height/shape.Height*25.4:0.0} DPI · {settings.Raster.Mode}. Preview is current; Apply saves these settings.";
            }
            catch(OperationCanceledException) { }
            catch(Exception ex) when(ex is InvalidDataException or ArgumentException) { if(!closed&&running==request)info.Text=ex.Message; }
            finally { next?.Dispose();if(running==request)running=null; }
        }
        void StartUpdate(){if(closed)return;running?.Cancel();jobs.RemoveAll(t=>t.IsCompleted);jobs.Add(Update());}
        void Changed()
        {
            if(closed||resetting)return;ready=null;apply.IsEnabled=false;running?.Cancel();debounce.Stop();
            prepared.Source=null;processed?.Dispose();processed=null;info.Text="Settings changed · preparing automatically…";debounce.Start();
        }
        debounce.Tick+=(_,_)=>{debounce.Stop();StartUpdate();};
        foreach(var input in new[]{brightness,contrast,gamma,threshold,spacing,overscan}) { input.ValueChanged+=(_,_)=>Changed(); input.PropertyChanged+=(_,e)=>{if(e.Property==NumericUpDown.TextProperty)Changed();}; }
        imageMode.SelectionChanged+=(_,_)=>Changed();invert.IsCheckedChanged+=(_,_)=>Changed();bidirectional.IsCheckedChanged+=(_,_)=>Changed();
        buttons.Children.Add(Button("Reset",()=>
        {
            resetting=true;
            try { imageMode.SelectedItem=originalOptions.Mode;brightness.Value=(decimal)originalOptions.Brightness;contrast.Value=(decimal)originalOptions.Contrast;gamma.Value=(decimal)originalOptions.Gamma;threshold.Value=originalOptions.Threshold;spacing.Value=(decimal)cut.Interval;overscan.Value=(decimal)originalOptions.Overscan;invert.IsChecked=originalOptions.Invert;bidirectional.IsChecked=originalOptions.Bidirectional; }
            finally { resetting=false;Changed(); }
        }));buttons.Children.Add(apply);buttons.Children.Add(Button("Cancel",()=>dialog.Close()));
        dialog.Content=root;dialog.Opened+=(_,_)=>StartUpdate();dialog.Closed+=(_,_)=>{closed=true;debounce.Stop();lifetime.Cancel();running?.Cancel();prepared.Source=null;processed?.Dispose();processed=null;};
        await dialog.ShowDialog(this);await Task.WhenAll(jobs);
    }
}
