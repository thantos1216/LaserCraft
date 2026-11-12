using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private Button? circleFrameButton;
    private readonly CheckBox frameBeforeRun=new() { Content="Rectangle frame before Run" };
    private async Task FrameAction(bool circle)
    {
        if(!CommitLayerEdits())return;
        if(monitor is {Simulation:null}){await RunDocumentJob(true,circle);return;}
        await OpenPreviewAsync(circle);
    }
}
