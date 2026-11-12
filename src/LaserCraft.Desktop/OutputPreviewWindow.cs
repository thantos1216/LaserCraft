using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;

public sealed class OutputPreviewWindow : Window
{
    private readonly DesignCanvas view=new() {ShowPreview=true,ShowTravel=true,PreviewSegmentLimit=200000};
    private readonly JobSimulation playback;
    private readonly DispatcherTimer timer=new() {Interval=TimeSpan.FromMilliseconds(40)};
    private readonly System.Diagnostics.Stopwatch clock=new();
    private readonly TextBlock state=new();
    private readonly Slider seek=new() {Minimum=0};
    private readonly NumericUpDown speed=new() {Minimum=1,Maximum=100,Value=10,Width=80,ShowButtonSpinner=false,Padding=new Thickness(6,3),FormatString="0"};
    private bool updating;
    private double previous;
    public OutputPreviewWindow(PreparedOutput prepared,bool inches,string title="Output preview")
    {
        Title="LaserCraft · "+title+" · "+prepared.Document.Name;Width=1040;Height=780;MinWidth=640;MinHeight=460;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        timer.Interval=TimeSpan.FromMilliseconds(prepared.Plan.Segments.Count>25000?200:40);
        playback=new(prepared.Plan);view.History.Reset(prepared.Document);view.Inches=inches;view.Plan=prepared.Plan;view.PreviewSeconds=double.PositiveInfinity;
        var root=new Grid {RowDefinitions=new("Auto,*,Auto,Auto,Auto"),Margin=new Thickness(12)};
        var controls=new WrapPanel {Orientation=Orientation.Horizontal};
        void Add(string label,Action action){var b=new Button {Content=label,Margin=new Thickness(3),Padding=new Thickness(9,5)};b.Click+=(_,_)=>action();controls.Children.Add(b);}
        Add("Play / resume",Play);Add("Pause",()=>{playback.Pause();timer.Stop();Update();});Add("Restart",()=>{playback.Stop();Play();});Add("Show all",()=>{timer.Stop();playback.Seek(prepared.Plan.Seconds);view.PreviewSeconds=double.PositiveInfinity;Update();});Add("Fit",view.Fit);Add("Back to Design",Close);
        controls.Children.Add(new TextBlock {Text="Speed ×",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0)});controls.Children.Add(speed);
        var travel=new CheckBox {Content="Show travel",IsChecked=true,Margin=new Thickness(8,0)};travel.IsCheckedChanged+=(_,_)=>{view.ShowTravel=travel.IsChecked==true;view.InvalidateVisual();};controls.Children.Add(travel);root.Children.Add(controls);
        Grid.SetRow(view,1);root.Children.Add(view);seek.Maximum=prepared.Plan.Seconds;seek.Value=prepared.Plan.Seconds;Grid.SetRow(seek,2);root.Children.Add(seek);
        seek.ValueChanged+=(_,_)=>{if(updating)return;timer.Stop();playback.Seek(seek.Value);view.PreviewSeconds=playback.Elapsed;Update();};
        state.Text="Full output";Grid.SetRow(state,3);root.Children.Add(state);
        var units=inches?"in":"mm";var summary=new TextBlock {Text=$"Snapshot · {prepared.Scope} · {prepared.Plan.Segments.Count} segments · {Measurements.ToDisplay(prepared.Plan.CutLength,inches):0.###} {units} cut · ideal {prepared.Plan.Seconds:0.0} s\nProcessed images and vectors are drawn with power-weighted black marks; dashed travel and orange head are optional. Timing excludes acceleration, material effects and live return. No machine commands are sent.",TextWrapping=Avalonia.Media.TextWrapping.Wrap,Margin=new Thickness(0,6)};Grid.SetRow(summary,4);root.Children.Add(summary);Content=root;
        timer.Tick+=(_,_)=>{var now=clock.Elapsed.TotalSeconds;playback.Advance((now-previous)*(double)(speed.Value??10));previous=now;view.PreviewSeconds=playback.Elapsed;Update();if(playback.State!=SimulationState.Running)timer.Stop();};
        KeyDown+=(_,e)=>{if(e.Key==Avalonia.Input.Key.Escape)Close();};Closed+=(_,_)=>{timer.Stop();view.ReleaseImages();};
    }
    private void Play(){playback.Start();view.PreviewSeconds=playback.Elapsed;clock.Restart();previous=0;timer.Start();Update();}
    private void Update(){updating=true;seek.Value=playback.Elapsed;updating=false;state.Text=$"{playback.State} · {playback.Elapsed:0.0} / {playback.Plan.Seconds:0.0} s";view.InvalidateVisual();}
}
