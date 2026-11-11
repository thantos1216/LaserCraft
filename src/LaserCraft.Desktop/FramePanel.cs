using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private Button? circleFrameButton;
    private readonly CheckBox frameBeforeRun=new() { Content="Rectangle frame before Run" };
    private async Task FrameAction(bool circle)
    {
        if(monitor is {Simulation:null}){await RunDocumentJob(true,circle);return;}
        await GeneratePreview(false);
        if(Current.Canvas.Plan is not { } plan || plan.Segments.Count==0)return;
        var frame=JobFootprint.Frame(JobFootprint.TrimInitialTravel(plan),circular:circle);
        Current.Canvas.Plan=new(frame.Segments.Select(s=>s with {Travel=true}).ToArray());Current.Canvas.ShowTravel=true;Current.Canvas.ShowPreview=true;Current.Canvas.PreviewSeconds=double.PositiveInfinity;Current.Canvas.InvalidateVisual();
        jobStatus.Text=circle?"Circle frame preview · no motion":"Rectangle frame preview · no motion";
    }
}
