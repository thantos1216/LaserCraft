using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private Task StartPhysicalOrPreview() { if(monitor is not null && monitor.Simulation is null)return ReviewDocumentJob();StartSimulation();return Task.CompletedTask; }
    private async Task PausePhysicalOrPreview()
    {
        var client=monitor;if(client?.JobActive==true){try{await client.SetJobPausedAsync(!client.JobPaused,connectionLifetime.Token);jobStatus.Text=client.JobPaused?"Physical hold requested · Resume continues":"Physical resume requested";}catch(Exception ex)when(ex is IOException or InvalidDataException or OperationCanceledException){Log(ex.Message);} }else PauseSimulation();
    }
    private async Task StopPhysicalOrPreview()
    {
        var client=monitor;if(client?.JobActive==true){try{await client.StopJobAsync();jobStatus.Text="Abort/reset requested · inspect output; reconnect required";}catch(Exception ex)when(ex is IOException or OperationCanceledException){Log("Abort transport failure: "+ex.Message);} }else StopSimulation();
    }
    private async Task ReviewDocumentJob()
    {
        var client=monitor;var profile=connectedProfile;if(client is null||profile is null||client.Simulation is not null||connecting||querying||disconnecting)return;
        if(!client.HomingConfirmed){status.Text="Use Home in this connection before document output.";return;}
        var session=Current;var document=session.Canvas.History.Document;var fingerprint=Snapshot(session);var scope=CurrentSimulationScope;var selected=cutSelected.IsChecked==true?session.Canvas.Selection.ToHashSet():null;
        ToolpathPlan plan; querying=true;UpdateConnectionControls();
        try{plan=await Task.Run(()=>ToolpathPlanner.Build(document,selected,RasterImages.EngravingGrid,connectionLifetime.Token),connectionLifetime.Token);plan=new(plan.Segments.SkipWhile(s=>s.Travel).ToArray());_ = GrblExport.Write(plan,profile);}
        catch(Exception ex)when(ex is InvalidDataException or OperationCanceledException){status.Text="Job preparation: "+ex.Message;return;}
        finally{querying=false;UpdateConnectionControls();}
        var points=plan.Segments.SelectMany(s=>new[]{s.From,s.To}).ToArray();var width=points.Max(p=>p.X)-points.Min(p=>p.X);var height=points.Max(p=>p.Y)-points.Min(p=>p.Y);
        var dialog=new Window {Title="Review physical document job",Width=640,Height=480,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel {Margin=new Avalonia.Thickness(18),Spacing=12};
        panel.Children.Add(Label("Physical vector / image job",18));panel.Children.Add(Label(profile.Name+" · "+client.Identity));
        panel.Children.Add(Label(FormattableString.Invariant($"{plan.Segments.Count} segments · {width:0.###} × {height:0.###} mm footprint · ideal {plan.Seconds:0.0}s. Enabled layers run in list order with their power, passes, raster processing and air settings. Selected-only: {selected is not null}.")));
        panel.Children.Add(Label("CURRENT HEAD anchors the minimum machine X/Y corner of the mapped output bounds. Job extends in +X/+Y; profile origin transforms the drawing first. No return move or work-offset changes. Supported machine range is [-profile travel,0]; jog inside it after Home. Homing is not work-zero setting. Ensure the entire footprint is over material and clear of limits; firmware soft limits may be off. Preview this document before running. This is a new streamer requiring physical qualification."));
        var ready=new CheckBox {Content="Placement/material/extraction checked; laser protection and physical stop ready"};panel.Children.Add(ready);var buttons=new WrapPanel();var run=Button("Run physical document",()=>dialog.Close(true));run.IsEnabled=false;ready.IsCheckedChanged+=(_,_)=>run.IsEnabled=ready.IsChecked==true;buttons.Children.Add(run);buttons.Children.Add(Button("Cancel",()=>dialog.Close(false)));panel.Children.Add(buttons);dialog.Content=new ScrollViewer {Content=panel};
        if(!await dialog.ShowDialog<bool>(this))return;
        if(monitor!=client||connectedProfile!=profile||Current!=session||Snapshot(session)!=fingerprint||CurrentSimulationScope!=scope||querying){status.Text="Connection, design or selection changed; review again.";return;}
        StopSimulation();querying=true;UpdateConnectionControls();jobStatus.Text="Physical job · preflight";
        var progress=new Progress<(int Sent,int Total)>(p=>{if(monitor!=client||!client.JobActive)return;jobProgress.Value=100d*p.Sent/p.Total;jobStatus.Text=$"Physical job · {p.Sent}/{p.Total} lines acknowledged (not motion completion)";});
        try{await client.ExecuteJobAsync(profile,plan,true,progress,connectionLifetime.Token);jobProgress.Value=100;jobStatus.Text="Job acknowledged; controller reported Idle. Inspect physical result.";Log(jobStatus.Text);}
        catch(Exception ex)when(ex is IOException or InvalidDataException or OperationCanceledException){Log("Physical job: "+ex.Message+" · no retry. Inspect output and save log.");jobStatus.Text="Physical job interrupted; inspect output";if(monitor==client && (client.RequiresReconnect||ex is IOException or OperationCanceledException))await DisconnectDevice();}
        finally{querying=false;UpdateConnectionControls();}
    }
}
