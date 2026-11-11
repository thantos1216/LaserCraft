using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private LaserCraft.Devices.GrblMonitor? explicitlyAbortedJob;
    private Task StartPhysicalOrPreview() { if(monitor is not null && monitor.Simulation is null)return ReviewDocumentJob();StartSimulation();return Task.CompletedTask; }
    private async Task PausePhysicalOrPreview()
    {
        var client=monitor;if(client?.JobActive==true){try{await client.SetJobPausedAsync(!client.JobPaused,connectionLifetime.Token);jobStatus.Text=client.JobPaused?"Physical hold requested · Resume continues":"Physical resume requested";}catch(Exception ex)when(ex is IOException or InvalidDataException or OperationCanceledException){Log(ex.Message);} }else PauseSimulation();
    }
    private async Task StopPhysicalOrPreview()
    {
        var client=monitor;if(client?.JobActive==true){explicitlyAbortedJob=client;try{await client.StopJobAsync();jobStatus.Text="Abort/reset requested · restoring connection after sender stops; no automatic homing";}catch(Exception ex)when(ex is IOException or OperationCanceledException){Log("Abort transport failure: "+ex.Message);} }else StopSimulation();
    }
    private async Task ReviewDocumentJob(bool frameOnly = false)
    {
        var client=monitor;var profile=connectedProfile;if(client is null||profile is null||client.Simulation is not null||connecting||querying||disconnecting)return;
        if(!client.HomingConfirmed){status.Text="Use Home in this connection before document output.";return;}
        var placement=PlacementRequest;var session=Current;var document=session.Canvas.History.Document;var fingerprint=Snapshot(session);var scope=CurrentSimulationScope;var selected=cutSelected.IsChecked==true?session.Canvas.Selection.ToHashSet():null;
        ToolpathPlan plan; MachineTravel travel; querying=true;UpdateConnectionControls();
        try{plan=await Task.Run(()=>ToolpathPlanner.Build(document,selected,RasterImages.EngravingGrid,connectionLifetime.Token),connectionLifetime.Token);plan=JobFootprint.TrimInitialTravel(plan);_ = GrblExport.Write(plan,profile);travel=client.ResolveMachineTravel(profile);}
        catch(Exception ex)when(ex is InvalidDataException or OperationCanceledException){status.Text="Job preparation: "+ex.Message;return;}
        finally{querying=false;UpdateConnectionControls();}
        var footprint=JobFootprint.Measure(plan);var width=footprint.Width;var height=footprint.Height;
        var dialog=new Window {Title=frameOnly ? "Review laser-off framing" : "Review physical document job",Width=640,Height=560,WindowStartupLocation=WindowStartupLocation.CenterOwner};var panel=new StackPanel {Margin=new Avalonia.Thickness(18),Spacing=12};
        panel.Children.Add(Label(frameOnly ? "Frame document · laser OFF" : "Physical vector / image job",18));panel.Children.Add(Label(profile.Name+" · "+client.Identity));panel.Children.Add(Label(FormattableString.Invariant($"Resolved machine travel: X[{travel.MinX:0.###},{travel.MaxX:0.###}] · Y[{travel.MinY:0.###},{travel.MaxY:0.###}] mm · {profile.CoordinateRange}")));
        panel.Children.Add(Label(FormattableString.Invariant($"{plan.Segments.Count} segments · {width:0.###} × {height:0.###} mm footprint · ideal {plan.Seconds:0.0}s. Enabled layers run in list order with their power, passes, raster processing and air settings. Selected-only: {selected is not null}.")));
        panel.Children.Add(Label($"Start From: {placement.Mode} - drawing anchor: {placement.Anchor}. Absolute Coordinates preserves drawing bed placement; relative modes place this drawing anchor at the current head or saved User Origin. Final bounds are checked using fresh machine position before output. No work offsets change."));
        if(placement.Mode==StartFromMode.UserOrigin && client.UserOrigin is null){status.Text="Set User Origin in Move before reviewing this mode.";return;}
        var frameFirst=new CheckBox { Content="Frame with laser OFF before cutting (returns to starting head position)", IsVisible=!frameOnly };panel.Children.Add(frameFirst);
        if(frameOnly)panel.Children.Add(Label("Frames the full prepared footprint at 600 mm/min with every S value zero, then returns to the captured starting position. Air ends off. This moves the machine; keep the path clear."));
        var ready=new CheckBox {Content="Placement/material/extraction checked; laser protection and physical stop ready"};panel.Children.Add(ready);var buttons=new WrapPanel();var run=Button(frameOnly ? "Frame with laser OFF" : "Run physical document",()=>dialog.Close(true));run.IsEnabled=false;ready.IsCheckedChanged+=(_,_)=>run.IsEnabled=ready.IsChecked==true;buttons.Children.Add(run);buttons.Children.Add(Button("Cancel",()=>dialog.Close(false)));panel.Children.Add(buttons);dialog.Content=new ScrollViewer {Content=panel};
        if(!await dialog.ShowDialog<bool>(this))return;
        if(monitor!=client||connectedProfile!=profile||Current!=session||Snapshot(session)!=fingerprint||CurrentSimulationScope!=scope||PlacementRequest!=placement||querying){status.Text="Connection, design or selection changed; review again.";return;}
        connectionJournal?.Add("INFO",$"Reviewed {(frameOnly ? "frame" : "document")} request · {plan.Segments.Count} segments · range {profile.CoordinateRange} - placement {placement.Mode}/{placement.Anchor}");
        StopSimulation();querying=true;UpdateConnectionControls();jobStatus.Text="Physical job · preflight";
        var progress=new Progress<(int Sent,int Total)>(p=>{if(monitor!=client||!client.JobActive)return;jobProgress.Value=100d*p.Sent/p.Total;jobStatus.Text=$"Physical job · {p.Sent}/{p.Total} lines acknowledged · {client.JobControllerState} (acknowledgements are not motion completion)";});
        try{if(!frameOnly && frameFirst.IsChecked==true)await client.ExecuteJobAsync(profile,plan,true,progress,connectionLifetime.Token,true,placement);await client.ExecuteJobAsync(profile,plan,true,progress,connectionLifetime.Token,frameOnly,placement);jobProgress.Value=100;jobStatus.Text=frameOnly?"Laser-off frame acknowledged; controller reported Idle. Inspect placement before burning.":"Job acknowledged; controller reported Idle. Inspect physical result.";Log(jobStatus.Text);}
        catch(Exception ex)when(ex is IOException or InvalidDataException or OperationCanceledException){connectionJournal?.Add("ERROR","Physical document/frame: "+ex.Message);Log("Physical job: "+ex.Message+" · no retry. Inspect output and save log.");jobStatus.Text="Physical job interrupted: "+ex.Message+" Inspect output; completion unknown.";if(monitor==client && (client.RequiresReconnect||ex is IOException or OperationCanceledException))await DisconnectDevice();}
        finally
        {
            querying=false;UpdateConnectionControls();
            if(explicitlyAbortedJob==client && !workspaceClosed && !connectionLifetime.IsCancellationRequested)
            {
                explicitlyAbortedJob=null;
                if(monitor==client)await DisconnectDevice();
                Log("Explicit Abort: reopening connection without homing or retrying the cut.");
                await ConnectDeviceCore(false,true);
                jobStatus.Text=monitor is not null?"Aborted · connection restored. Read status; Home if Alarm. Cut was not resumed.":"Aborted · reconnect failed; check USB and use Connect. Cut was not resumed.";
            }
        }
    }
}
