using Avalonia.Controls;
using LaserCraft.Devices;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private async Task ReviewLineTest()
    {
        var client = monitor; var profile = connectedProfile;
        if (client is null || profile is null || querying || !PhysicalControlsAvailable) return;
        var dialog = new Window { Title = "Review physical line test", Width = 650, Height = 550, MinWidth = 550, MinHeight = 480, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var axis = new ComboBox { ItemsSource = new[] { "X axis", "Y axis" }, SelectedIndex = 0 };
        var length = Number(-20,20); length.Value = 5; length.Increment = 1;
        var feed = Number(60,3000); feed.Value = 600; var power = Number(.001m,100); power.Value = 10;
        var ready = new CheckBox { Content = "Head positioned over scrap; signed path clear; laser protection and physical stop ready" };
        var info = Label(""); GrblManualRequest? prepared = null; Button run = null!;
        void Update()
        {
            prepared = null; run.IsEnabled = false;
            try
            {
                var request = new GrblManualRequest(axis.SelectedIndex == 0 ? GrblManualAction.CutLineX : GrblManualAction.CutLineY, (double)(length.Value ?? 0), (double)(feed.Value ?? 0), (double)(power.Value ?? 0), ClearPathConfirmed: ready.IsChecked == true); request.Validate(); prepared = request;
                info.Text = FormattableString.Invariant($"One pass from CURRENT head position: {(axis.SelectedIndex == 0 ? "X" : "Y")} {request.Distance:+0.###;-0.###} mm · {request.Feed:0.###} mm/min · {request.PowerPercent:0.###}% (requested S{Math.Floor(profile.MaxPower * request.PowerPercent / 100):0}). No return move.\nNo material recipe or guaranteed through-cut. Defaults are only a motion/mark test; 5 mm plywood requires your own qualification. Air state is unchanged.");
                run.IsEnabled = ready.IsChecked == true;
            }
            catch (InvalidDataException ex) { info.Text = ex.Message; }
        }
        run = Button("Run this physical line test", () => { if (prepared is not null && ready.IsChecked == true) dialog.Close(prepared); });
        var panel = new StackPanel { Margin = new Avalonia.Thickness(18), Spacing = 8 };
        panel.Children.Add(Label("Supervised one-line qualification",18)); panel.Children.Add(Label(profile.Name + " · " + client.Identity));
        panel.Children.Add(Label("This moves and fires the laser. Verify X/Y directions with unpowered jogs first. A 430 × 430 profile or displayed zero does not establish clearance/homing; firmware soft limits may be off. Start well inside the work area. Software off/disconnect is not an emergency stop."));
        var fields = new WrapPanel(); Inline(fields,"Axis",axis,110); Inline(fields,"Signed length · mm",length,140); Inline(fields,"Feed · mm/min",feed,135); Inline(fields,"Power · %",power,110); panel.Children.Add(fields);
        panel.Children.Add(info); panel.Children.Add(ready); var actions = new WrapPanel(); actions.Children.Add(run); actions.Children.Add(Button("Cancel",()=>dialog.Close())); panel.Children.Add(actions);
        length.ValueChanged += (_,_)=>Update();feed.ValueChanged += (_,_)=>Update();power.ValueChanged += (_,_)=>Update();axis.SelectionChanged += (_,_)=>Update();ready.IsCheckedChanged += (_,_)=>Update();dialog.Content = new ScrollViewer {Content=panel};Update();
        var accepted = await dialog.ShowDialog<GrblManualRequest?>(this); if (accepted is null) return;
        if (monitor != client || connectedProfile != profile || querying || disconnecting || connecting) { manualStatus.Text = "Connection changed; reopen the line test."; return; }
        await PhysicalAction(accepted);
    }
}
