using Avalonia.Controls;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private readonly TextBlock connectionSummary=new() {FontSize=11,MaxLines=1,TextTrimming=Avalonia.Media.TextTrimming.CharacterEllipsis};
    private void RefreshConnectionSummary()
    {
        connectionSummary.Text=connecting?"Connecting / homing…":disconnecting?"Disconnecting…":monitor?.JobActive==true?"Job · "+monitor.JobControllerState:querying?"Working…":monitor is null?"Disconnected":"Last report · "+connectionStatus.Text?.Split('\n')[0];
        ToolTip.SetTip(connectionSummary,connectionStatus.Text);
    }
    private void ShowConnectionDetails()
    {
        var report=new TextBox {IsReadOnly=true,AcceptsReturn=true,TextWrapping=Avalonia.Media.TextWrapping.Wrap,Margin=new Avalonia.Thickness(12)};report.Bind(TextBox.TextProperty,new Avalonia.Data.Binding("Text"){Source=connectionStatus});
        new Window {Title="LaserCraft · Connection details",Width=700,Height=360,Content=report,WindowStartupLocation=WindowStartupLocation.CenterOwner}.Show(this);
    }
}
