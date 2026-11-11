using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private readonly ComboBox startFrom=new() { ItemsSource=new[]{"Absolute Coordinates","Current Position","User Origin"}, SelectedIndex=1 };
    private readonly ComboBox jobAnchor=new() { ItemsSource=Enum.GetValues<JobOriginAnchor>(), SelectedIndex=6 };
    private JobPlacementRequest PlacementRequest=>new((StartFromMode)startFrom.SelectedIndex,(JobOriginAnchor)jobAnchor.SelectedIndex);
    private readonly TextBlock originStatus=new() { TextWrapping=Avalonia.Media.TextWrapping.Wrap };
    private readonly NumericUpDown positionX=Number(0,10000),positionY=Number(0,10000);
    private readonly List<Button> positionButtons=[];
    private Control BuildPlacementPanel()
    {
        var panel=new StackPanel { Spacing=4 };var row=new WrapPanel();Inline(row,"Start From",startFrom,180);Inline(row,"Job Origin (drawing anchor)",jobAnchor,180);panel.Children.Add(row);panel.Children.Add(originStatus);
        startFrom.SelectionChanged+=(_,_)=>jobAnchor.IsEnabled=startFrom.SelectedIndex!=0;
        panel.Children.Add(Label("Absolute uses drawing bed placement. Current Position places the chosen drawing anchor at the head. User Origin uses a saved head position. Selected-only uses selected output bounds."));return panel;
    }
    private Control BuildPositionControls()
    {
        var panel=new StackPanel { Spacing=4 };panel.Children.Add(Label("Exact bed position - Home first",16));var row=new WrapPanel();Inline(row,"Bed X - mm",positionX,110);Inline(row,"Bed Y - mm",positionY,110);panel.Children.Add(row);var actions=new WrapPanel();
        void Add(string title,Func<Task> action){var b=AsyncButton(title,action);positionButtons.Add(b);actions.Children.Add(b);}
        Add("Go to bed X/Y",()=>PositionAction(new((double)(positionX.Value??0),(double)(positionY.Value??0))));Add("Go to bed 0,0",()=>PositionAction(new(0,0)));Add("Set User Origin here",()=>PositionAction(null,true));Add("Read position",()=>PositionAction());
        panel.Children.Add(actions);panel.Children.Add(Label("Bed coordinates are measured from the configured device origin. Absolute machine jogs use laser OFF and the jog feed above. 0,0 may be at a limit; allow clearance. User Origin is session-only, cleared by Home/reconnect. No firmware work offsets change."));return panel;
    }
    private async Task PositionAction(PointMm? target=null,bool capture=false)
    {
        var client=monitor;var profile=connectedProfile;if(client is null||profile is null||querying||connecting||disconnecting)return;querying=true;UpdateConnectionControls();
        try{var p=await client.PositionAsync(profile,target,(double)(jogFeed.Value??600),capture,connectionLifetime.Token);manualTranscript.Text=FormattableString.Invariant($"{(capture?"Saved User Origin":target is null?"Reported machine position":"Commanded target (acknowledged; read position after movement)")}: X {p.X:0.######}, Y {p.Y:0.######}");Log(manualTranscript.Text);}
        catch(Exception ex)when(ex is IOException or InvalidDataException or OperationCanceledException){Log("Position: "+ex.Message);manualTranscript.Text=ex.Message;if(client.RequiresReconnect||ex is IOException or OperationCanceledException)await DisconnectDevice();}
        finally{querying=false;UpdateConnectionControls();}
    }
    private void UpdatePlacementControls()
    {
        foreach(var b in positionButtons)b.IsEnabled=PhysicalControlsAvailable&&!querying&&monitor?.HomingConfirmed==true;
        originStatus.Text=monitor?.UserOrigin is PointMm p?FormattableString.Invariant($"User Origin - machine X {p.X:0.###}, Y {p.Y:0.###} mm"):"User Origin: not set in this connection";
    }
}
