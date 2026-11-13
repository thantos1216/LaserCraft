using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private readonly ComboBox outputScope=new() {ItemsSource=new[]{"All enabled artwork","Selected objects only"},SelectedIndex=0};
    private bool SelectedOnly=>outputScope.SelectedIndex==1;
    private CancellationTokenSource? previewBuild;
    private OutputPreviewWindow? outputPreview;
    private HashSet<Guid>? ResolveOutputSelection()=>PreparedOutput.ResolveSelection(SelectedOnly,Current.Canvas.Selection);
    private void CancelPreviewPreparation()=>previewBuild?.Cancel();
    private void Preview()=>_=OpenPreviewAsync();
    private async Task<PreparedOutput> PrepareCurrentOutputAsync(CancellationToken token)
    {
        var session=Current;var document=session.Canvas.History.Document;var fingerprint=Snapshot(session);var scope=CurrentOutputScope;var selected=ResolveOutputSelection();
        await previewGate.WaitAsync(token);
        try
        {
            var prepared=await Task.Run(()=>PreparedOutput.Create(document,selected,RasterImages.EngravingGrid,token),token);
            if(workspaceClosed||Current!=session||Snapshot(session)!=fingerprint||CurrentOutputScope!=scope)throw new InvalidDataException("Design or selection changed; prepare output again.");
            return prepared;
        }
        finally{previewGate.Release();}
    }
    private async Task OpenPreviewAsync(bool? circularFrame=null)
    {
        if(!CommitLayerEdits())return;CancelPreviewPreparation();
        using var request=CancellationTokenSource.CreateLinkedTokenSource(connectionLifetime.Token);previewBuild=request;
        try
        {
            status.Text="Preparing preview…";var prepared=await PrepareCurrentOutputAsync(request.Token);request.Token.ThrowIfCancellationRequested();
            if(circularFrame is bool circle){var frame=JobFootprint.Frame(JobFootprint.TrimInitialTravel(prepared.Plan),circular:circle);prepared=prepared with {Plan=new(frame.Segments.Select(s=>s with {Travel=true}).ToArray())};}
            outputPreview?.Close();var window=outputPreview=new OutputPreviewWindow(prepared,inches,circularFrame is null?"Output preview":circularFrame==true?"Circle frame preview":"Rectangle frame preview");
            window.Closed+=(_,_)=>{if(outputPreview==window)outputPreview=null;};window.Show(this);status.Text="Preview opened separately; design remains editable.";
        }
        catch(OperationCanceledException){if(!workspaceClosed)status.Text="Preview preparation cancelled.";}
        catch(Exception ex)when(ex is InvalidDataException or ArgumentException)
        {
            if(workspaceClosed||request.IsCancellationRequested)return;status.Text="Preview: "+ex.Message;
            var message=new Window {Title="Preview unavailable",Width=540,Height=220,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Background,Foreground=Foreground};var panel=new StackPanel {Margin=new Avalonia.Thickness(16),Spacing=8};panel.Children.Add(Label(ex.Message));panel.Children.Add(Button("Close",message.Close));message.Content=panel;await message.ShowDialog(this);
        }
        finally{if(previewBuild==request)previewBuild=null;}
    }
}
