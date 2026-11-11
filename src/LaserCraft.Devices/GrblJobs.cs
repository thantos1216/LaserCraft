using System.Globalization;
using LaserCraft.Domain;
namespace LaserCraft.Devices;
public sealed partial class GrblMonitor
{
    private CancellationTokenSource? jobCancellation;
    private readonly object jobControl = new();
    private int jobActive, jobPaused, jobReset;
    public MachineTravel ResolveMachineTravel(DeviceProfile profile) { if(!HomingConfirmed)throw new InvalidDataException("Home in this connection before resolving machine travel.");return MachineTravel.Resolve(profile,homedMachinePosition); }
    public string JobControllerState { get; private set; } = "";
    public bool JobActive => Volatile.Read(ref jobActive) != 0;
    public bool JobPaused => Volatile.Read(ref jobPaused) != 0;
    public async Task SetJobPausedAsync(bool paused, CancellationToken token = default)
    {
        if (!JobActive) throw new InvalidDataException("No physical job is active.");
        Volatile.Write(ref jobPaused, paused ? 1 : 0);
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(2));
        await transport.WriteAsync(paused ? "!" : "~",timeout.Token);
    }
    private async Task ResetJobAsync()
    {
        if (Interlocked.Exchange(ref jobReset,1) != 0) return;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(2));
        Volatile.Write(ref homingConfirmed,0);Volatile.Write(ref consoleUncertain,1); await transport.WriteAsync("\u0018",timeout.Token);
    }
    public async Task StopJobAsync()
    {
        if (!JobActive) return;
        lock(jobControl) { if(jobCancellation is null)return; jobCancellation.Cancel(); }
        await ResetJobAsync();
    }
    public async Task ExecuteJobAsync(DeviceProfile profile, ToolpathPlan plan, bool reviewedPlacement, IProgress<(int Sent,int Total)>? progress = null, CancellationToken token = default, bool frameOnly = false, JobPlacementRequest? placement = null)
    {
        profile.Validate();placement ??= new();
        plan = JobFootprint.TrimInitialTravel(plan);
        if (!reviewedPlacement || profile.Controller != ControllerFamily.Grbl || profile.Connection == ConnectionKind.Simulator || !Identity.StartsWith("[VER:1.1",StringComparison.Ordinal) && !Identity.StartsWith("Grbl 1.1",StringComparison.Ordinal)) throw new InvalidDataException("Reviewed physical GRBL 1.1 placement is required.");
        // Validate/export once before acquiring resources; final export uses the fresh machine anchor.
        _ = GrblExport.Write(plan,profile);
        if (Interlocked.CompareExchange(ref jobActive,1,0) != 0) throw new InvalidDataException("A physical job is already active.");
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token); lock(jobControl)jobCancellation=stop; Volatile.Write(ref jobPaused,0); Volatile.Write(ref jobReset,0);
        bool acquired=false, started=false; int sent=0,total=0; JobControllerState="Preflight";
        try
        {
            await gate.WaitAsync(stop.Token); acquired=true; RequireSynchronizedConsole();
            if(!HomingConfirmed)throw new InvalidDataException("Home in this connection before document output. Current placement requires a verified homing reference.");
            async Task<GrblConsoleResult> Command(string text)
            {
                using var deadline=CancellationTokenSource.CreateLinkedTokenSource(stop.Token);deadline.CancelAfter(TimeSpan.FromSeconds(120));
                var reply=await ExchangeConsoleCore(GrblConsoleCommand.Prepare(text,true),deadline.Token,status=>{JobControllerState=status.State;if(total>0)progress?.Report((sent,total));});
                if(reply.Outcome is GrblConsoleOutcome.Rejected or GrblConsoleOutcome.Alarm) throw new IOException("Job command rejected: "+reply.Lines[^1]);
                return reply;
            }
            var settingsReply=await Command("$$");var settings=new Dictionary<int,double>();var seenSettings=new HashSet<int>();
            foreach(var line in settingsReply.Lines.Where(l=>l.StartsWith('$'))){var setting=GrblSetting.Parse(line);if(!seenSettings.Add(setting.Number))throw new InvalidDataException("Duplicate job setting.");if(!double.TryParse(setting.Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var value)||!double.IsFinite(value))continue;if(!settings.TryAdd(setting.Number,value))throw new InvalidDataException("Duplicate job setting.");}
            bool EqualsSetting(int id,double value)=>settings.TryGetValue(id,out var reported)&&reported==value;
            if(!EqualsSetting(13,0)||!EqualsSetting(30,profile.MaxPower)||!EqualsSetting(31,0)||!EqualsSetting(32,1)||!EqualsSetting(130,profile.BedWidth)||!EqualsSetting(131,profile.BedHeight))throw new InvalidDataException("Job requires report-mm $13=0, matching profile $30/$130/$131, $31=0 and $32=1. Review settings/profile; nothing changed automatically.");
            var snapshot=await Command("?");var state=GrblStatus.Parse(snapshot.Lines.Last(l=>l.StartsWith('<')));
            if(state.State!="Idle"||state.Coordinates!="MPos"||state.X is null||state.Y is null)throw new InvalidDataException("Job requires fresh Idle machine position in millimetres.");
            var travel=ResolveMachineTravel(profile);
            var resolved=JobPlacement.Resolve(plan,profile,travel,new(state.X.Value,state.Y.Value),userOrigin,placement);
            var program=GrblExport.WritePlaced(frameOnly ? JobFootprint.Frame(plan) : plan,profile,resolved);
            if(frameOnly) program += FormattableString.Invariant($"G53 G0 X{state.X.Value:0.######} Y{state.Y.Value:0.######} S0\n");
            var lines=program.Split('\n',StringSplitOptions.RemoveEmptyEntries).Where(l=>!l.StartsWith(';')).ToArray();
            foreach(var line in lines)GrblConsoleCommand.Prepare(line,true);total=lines.Length;
            // One line/ack at a time; this deliberately trades raster throughput for a bounded sender.
            for(int i=0;i<lines.Length;i++)
            {
                while(JobPaused)await Task.Delay(50,stop.Token);
                stop.Token.ThrowIfCancellationRequested();started=true;JobControllerState="Waiting for acknowledgement";await Command(lines[i]);sent=i+1;progress?.Report((sent,total));
            }
            // M5/M9 terminal acknowledgements are followed by controller-reported completion.
            for(int i=0;i<1200;i++)
            {
                while(JobPaused)await Task.Delay(50,stop.Token);
                var reply=await Command("?");var final=GrblStatus.Parse(reply.Lines.Last(l=>l.StartsWith('<')));
                if(final.State=="Idle")return;
                if(final.State is not ("Run" or "Hold:0" or "Hold:1"))throw new IOException("Unexpected completion state: "+final.State);
                await Task.Delay(100,stop.Token);
            }
            throw new IOException("Job did not report Idle within completion limit.");
        }
        catch
        {
            if(started){Volatile.Write(ref consoleUncertain,1);try{await ResetJobAsync();}catch(IOException){}catch(OperationCanceledException){}catch(ObjectDisposedException){} }
            throw;
        }
        finally
        {
            if(Volatile.Read(ref jobReset)!=0)Volatile.Write(ref consoleUncertain,1);
            if(acquired)gate.Release();lock(jobControl)jobCancellation=null;Volatile.Write(ref jobActive,0);Volatile.Write(ref jobPaused,0);
        }
    }
}
