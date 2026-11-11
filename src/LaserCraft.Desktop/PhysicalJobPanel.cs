using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private LaserCraft.Devices.GrblMonitor? explicitlyAbortedJob;
    private async Task PausePhysicalOrPreview()
    {
        var client=monitor;if(client?.JobActive==true){try{await client.SetJobPausedAsync(!client.JobPaused,connectionLifetime.Token);jobStatus.Text=client.JobPaused?"Physical hold requested · Resume continues":"Physical resume requested";}catch(Exception ex)when(ex is IOException or InvalidDataException or OperationCanceledException){Log(ex.Message);} }else if(simulation?.State==SimulationState.Paused)StartSimulation();else PauseSimulation();
    }
    private async Task StopPhysicalOrPreview()
    {
        var client=monitor;if(client?.JobActive==true){explicitlyAbortedJob=client;try{await client.StopJobAsync();jobStatus.Text="Abort/reset requested · restoring connection after sender stops; no automatic homing";}catch(Exception ex)when(ex is IOException or OperationCanceledException){Log("Abort transport failure: "+ex.Message);} }else StopSimulation();
    }
    private async Task RunDocumentJob(bool frameOnly = false,bool circular=false)
    {
        var client=monitor;var profile=connectedProfile;if(client is null||profile is null||client.Simulation is not null||connecting||querying||disconnecting)return;
        if(!client.HomingConfirmed){status.Text="Use Home in this connection before document output.";return;}
        var placement=PlacementRequest;var session=Current;var document=session.Canvas.History.Document;var fingerprint=Snapshot(session);var scope=CurrentSimulationScope;var selected=cutSelected.IsChecked==true?session.Canvas.Selection.ToHashSet():null;
        ToolpathPlan plan; MachineTravel travel; querying=true;UpdateConnectionControls();
        try{plan=await Task.Run(()=>ToolpathPlanner.Build(document,selected,RasterImages.EngravingGrid,connectionLifetime.Token),connectionLifetime.Token);plan=JobFootprint.TrimInitialTravel(plan);_ = GrblExport.Write(plan,profile);travel=client.ResolveMachineTravel(profile);}
        catch(Exception ex)when(ex is InvalidDataException or OperationCanceledException){status.Text="Job preparation: "+ex.Message;return;}
        finally{querying=false;UpdateConnectionControls();}
        if(placement.Mode==StartFromMode.UserOrigin && client.UserOrigin is null){status.Text="Set User Origin in Move first.";return;}
        if(monitor!=client||connectedProfile!=profile||Current!=session||Snapshot(session)!=fingerprint||CurrentSimulationScope!=scope||PlacementRequest!=placement||querying){status.Text="Connection, design or selection changed; review again.";return;}
        connectionJournal?.Add("INFO",$"Run {(frameOnly ? "frame" : "document")} request · {plan.Segments.Count} segments · range {profile.CoordinateRange} - placement {placement.Mode}/{placement.Anchor}");
        StopSimulation();querying=true;UpdateConnectionControls();jobStatus.Text="Physical job · preflight";
        var progress=new Progress<(int Sent,int Total)>(p=>{if(monitor!=client||!client.JobActive)return;jobProgress.Value=100d*p.Sent/p.Total;jobStatus.Text=$"Physical job · {p.Sent}/{p.Total} lines acknowledged · {client.JobControllerState} (acknowledgements are not motion completion)";});
        try{if(!frameOnly && frameBeforeRun.IsChecked==true)await client.ExecuteJobAsync(profile,plan,true,progress,connectionLifetime.Token,true,placement,circular);await client.ExecuteJobAsync(profile,plan,true,progress,connectionLifetime.Token,frameOnly,placement,circular);jobProgress.Value=100;jobStatus.Text=frameOnly?"Laser-off frame acknowledged; controller reported Idle. Inspect placement before burning.":"Job acknowledged; controller reported Idle. Inspect physical result.";Log(jobStatus.Text);}
        catch(Exception ex)when(ex is IOException or InvalidDataException or OperationCanceledException){connectionJournal?.Add("ERROR","Physical document/frame: "+ex.Message);Log("Physical job: "+ex.Message+" · no retry. Inspect output and save log.");jobStatus.Text="Physical job interrupted: "+ex.Message+" Inspect output; completion unknown.";if(monitor==client && (client.RequiresReconnect||ex is IOException or OperationCanceledException))await DisconnectDevice();}
        finally
        {
            querying=false;UpdateConnectionControls();
            if(explicitlyAbortedJob==client && !workspaceClosed && !connectionLifetime.IsCancellationRequested)
            {
                explicitlyAbortedJob=null;
                if(monitor==client)await DisconnectDevice();
                Log("Explicit Abort: reopening connection without homing or retrying the cut.");
                await (connectionOperation=ConnectDeviceCore(false,true));
                jobStatus.Text=monitor is not null?"Aborted · connection restored. Read status; Home if Alarm. Cut was not resumed.":"Aborted · reconnect failed; check USB and use Connect. Cut was not resumed.";
            }
        }
    }
}
