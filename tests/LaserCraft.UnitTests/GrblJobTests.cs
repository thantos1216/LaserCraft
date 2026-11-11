using System.Threading.Channels;
using LaserCraft.Domain;
using LaserCraft.Devices;
namespace LaserCraft.UnitTests;
public sealed class GrblJobTests
{
    private static DeviceProfile Profile=>DeviceProfile.Simulator() with {Connection=ConnectionKind.Serial,SerialPort="COM5",BedWidth=430,BedHeight=430};
    private static ToolpathPlan Plan()=>ToolpathPlanner.Build(DesignDocument.Empty() with {Width=430,Height=430,Shapes=[new(Guid.NewGuid(),ShapeKind.Rectangle,10,10,5,5)]});
    [Fact] public async Task VectorJobUsesFreshNegativeMachineAnchorAndReportsCompletion()
    {
        var t=new Peer();await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);await c.ExecuteJobAsync(Profile,Plan(),true);
        Assert.Contains("G53 G0 X-59 Y-70 S0\n",t.Commands);Assert.Contains("G53 G1 X-54 Y-75 F1200 S200\n",t.Commands);Assert.Equal("?",t.Commands[^1]);Assert.False(c.RequiresReconnect);Assert.False(c.JobActive);
        Assert.DoesNotContain(t.Commands,s=>s.Contains("G10")||s.Contains("G92"));
    }
    [Fact] public async Task RasterJobStreamsProcessedPowerAndDedicatedLayerSettings()
    {
        var asset=new RasterAsset(Guid.NewGuid(),"image/png","AAAA",2,1);var d=DesignDocument.Empty() with {Width=430,Height=430,Shapes=[new(Guid.NewGuid(),ShapeKind.Image,10,10,2,1,1,asset)]};d.Layers.Add(new("Image","#000000",Speed:600,Power:50,Mode:CutMode.Image,Kind:LayerKind.Image,Interval:1,Raster:new(RasterMode.Grayscale)));
        var plan=ToolpathPlanner.Build(d,null,(_,_,_)=>new RasterGrid(2,1,[255,128]));var t=new Peer();await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);await c.ExecuteJobAsync(Profile,plan,true);
        Assert.Contains(t.Commands,s=>s.StartsWith("G53 G1",StringComparison.Ordinal)&&s.Contains("S500"));Assert.Contains("M5\n",t.Commands);Assert.Contains("M9\n",t.Commands);
    }
    [Fact] public async Task PauseResumeAndAbortUseRealtimeBytesWhileOneReaderOwnsAcks()
    {
        var t=new Peer {HoldFirstMotion=true};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);var job=c.ExecuteJobAsync(Profile,Plan(),true);await t.Motion.Task;
        await c.SetJobPausedAsync(true);Assert.True(c.JobPaused);await c.SetJobPausedAsync(false);Assert.False(c.JobPaused);Assert.Contains("!",t.Commands);Assert.Contains("~",t.Commands);
        await c.StopJobAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>job);Assert.Single(t.Commands,s=>s=="\u0018");Assert.True(c.RequiresReconnect);Assert.False(c.JobActive);
    }
    [Theory] [InlineData("$13=1")] [InlineData("$30=999")] [InlineData("$32=0")] [InlineData("$130=400")]
    public async Task MismatchedSettingsDoNotDispatchProgram(string replacement)
    {
        var t=new Peer {Replacement=replacement};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteJobAsync(Profile,Plan(),true));Assert.DoesNotContain(t.Commands,s=>s.StartsWith("G53",StringComparison.Ordinal));Assert.DoesNotContain("\u0018",t.Commands);
    }
    [Fact] public async Task FailedMotionResetsAndNeverRetriesOrReportsSuccess()
    {
        var t=new Peer {RejectMotion=true};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);await Assert.ThrowsAsync<IOException>(()=>c.ExecuteJobAsync(Profile,Plan(),true));Assert.Single(t.Commands,s=>s.StartsWith("G53",StringComparison.Ordinal));Assert.Contains("\u0018",t.Commands);Assert.True(c.RequiresReconnect);
    }
    [Fact] public async Task ClearanceAndPreCancellationDoNotWriteProgram()
    {
        var t=new Peer();await using var c=await GrblMonitor.ConnectAsync(t);await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteJobAsync(Profile,Plan(),false));using var cancel=new CancellationTokenSource();cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>c.ExecuteJobAsync(Profile,Plan(),true,token:cancel.Token));Assert.Single(t.Commands);
    }
    [Fact] public async Task HomeReadsHomingSettingAndDoesNotSetWorkZero()
    {
        var t=new Peer();await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);var result=await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);Assert.Contains("$H\n",t.Commands);Assert.Contains("work offsets unchanged",result.Summary);Assert.DoesNotContain(t.Commands,s=>s.Contains("G10")||s.Contains("G92"));
    }
    [Fact] public async Task LargerJogUsesAxisProfileLimitInsteadOfTenMillimetres()
    {
        var t=new Peer();await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);await c.ExecuteManualAsync(Profile,new(GrblManualAction.JogX,15),true);Assert.Contains("$J=G21 G91 X15 F600\n",t.Commands);await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteManualAsync(Profile,new(GrblManualAction.JogY,431),true));
    }
    [Fact]public async Task DisabledHomingDoesNotSendHome()
    {
        var t=new Peer {Replacement="$22=0"};await using var c=await GrblMonitor.ConnectAsync(t);await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true));Assert.DoesNotContain("$H\n",t.Commands);
    }
    [Fact]public async Task PausePreventsFurtherDispatchUntilResume()
    {
        var t=new Peer {HoldFirstMotion=true};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);var job=c.ExecuteJobAsync(Profile,Plan(),true);await t.Motion.Task;await c.SetJobPausedAsync(true);var count=t.Commands.Count;t.Reply("ok");await Task.Delay(80);Assert.Equal(count,t.Commands.Count);Assert.False(job.IsCompleted);await c.SetJobPausedAsync(false);await job;Assert.False(c.RequiresReconnect);
    }
    [Fact]public async Task DisconnectCancelsJobAndQueuedQueries()
    {
        var t=new Peer {HoldFirstMotion=true};var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);var job=c.ExecuteJobAsync(Profile,Plan(),true);await t.Motion.Task;var queued=c.QueryStatusAsync();await c.DisposeAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>job);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>queued);Assert.False(c.JobActive);
    }
    [Fact]public async Task JobsRequireHomeInTheCurrentConnection()
    {
        var t=new Peer();await using var c=await GrblMonitor.ConnectAsync(t);await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteJobAsync(Profile,Plan(),true));Assert.Single(t.Commands);
    }
    [Fact]public async Task FootprintNearMachineZeroIsBlockedBeforeMotion()
    {
        var t=new Peer {MachineX=-1,MachineY=-1};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteJobAsync(Profile,Plan(),true));Assert.DoesNotContain(t.Commands,s=>s.StartsWith("G53",StringComparison.Ordinal));
    }
    [Fact]public async Task CustomConsoleWritesInvalidateHomingEvidence()
    {
        var t=new Peer();await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);Assert.True(c.HomingConfirmed);await c.SubmitConsoleAsync("M5",true);Assert.False(c.HomingConfirmed);await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteJobAsync(Profile,Plan(),true));
    }
    [Fact]public async Task FramingSendsOnlyZeroPowerAndReturnsToCapturedAnchor()
    {
        var t=new Peer();await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);
        var before=t.Commands.Count;await c.ExecuteJobAsync(Profile,Plan(),true,frameOnly:true);var sent=t.Commands.Skip(before).ToArray();
        Assert.Contains("G53 G0 X-59 Y-75 S0\n",sent);Assert.All(sent.Where(l=>l.Contains(" S")),l=>Assert.EndsWith(" S0\n",l));Assert.DoesNotContain("M8\n",sent);Assert.DoesNotContain("M7\n",sent);Assert.Equal("?",sent[^1]);Assert.True(c.HomingConfirmed);
    }
    [Fact]public void FrameKeepsFootprintAndRejectsInvalidFeed()
    {
        var plan=JobFootprint.TrimInitialTravel(Plan());var frame=JobFootprint.Frame(plan);Assert.Equal(JobFootprint.Measure(plan),JobFootprint.Measure(frame));Assert.All(frame.Segments,s=>{Assert.Equal(0,s.Power);Assert.False(s.AirAssist);});Assert.Throws<InvalidDataException>(()=>JobFootprint.Frame(plan,0));Assert.Throws<InvalidDataException>(()=>JobFootprint.Measure(new ToolpathPlan([])));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task PositiveHomingAndLoggedPositionPermitPoweredJobAndFrame(bool frame)
    {
        var t=new Peer {MachineX=1,MachineY=1};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);
        t.MachineX=101;t.MachineY=101;var before=t.Commands.Count;await c.ExecuteJobAsync(Profile,Plan(),true,frameOnly:frame);var sent=t.Commands.Skip(before).ToArray();
        Assert.Contains(sent,l=>l.StartsWith("G53 G1 X106",StringComparison.Ordinal));Assert.Equal("?",sent[^1]);Assert.False(c.RequiresReconnect);
        if(frame){Assert.Contains("G53 G0 X101 Y101 S0\n",sent);Assert.All(sent.Where(l=>l.Contains(" S")),l=>Assert.EndsWith(" S0\n",l));}else Assert.Contains(sent,l=>l.EndsWith(" S200\n",StringComparison.Ordinal));
    }
    [Fact]public void HomingSignResolvesPositiveNegativeMixedAndExplicitOverride()
    {
        var positive=MachineTravel.Resolve(Profile,new(1,1));Assert.Equal(new MachineTravel(0,430,0,430),positive);Assert.True(positive.Contains(new(101,101),new(0,0,25,10)));Assert.False(positive.Contains(new(420,420),new(0,0,25,10)));
        Assert.Equal(new MachineTravel(-430,0,-430,0),MachineTravel.Resolve(Profile,new(-1,-1)));
        Assert.Equal(new MachineTravel(0,430,-430,0),MachineTravel.Resolve(Profile,new(1,-1)));
        Assert.Throws<InvalidDataException>(()=>MachineTravel.Resolve(Profile,new(0,0)));Assert.Throws<InvalidDataException>(()=>MachineTravel.Resolve(Profile,null));
        Assert.Equal(positive,MachineTravel.Resolve(Profile with {CoordinateRange=MachineCoordinateRange.Positive},new(0,0)));
        Assert.Equal(new MachineTravel(-430,0,-430,0),MachineTravel.Resolve(Profile with {CoordinateRange=MachineCoordinateRange.Negative},null));
    }
    [Fact]public async Task PositionAndSavedOriginUseFreshReportsAndNeverChangeWorkOffsets()
    {
        var t=new Peer {MachineX=1,MachineY=1};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);
        t.MachineX=100;t.MachineY=100;Assert.Equal(new PointMm(100,100),await c.PositionAsync(Profile,captureOrigin:true));
        t.MachineX=200;t.MachineY=200;await c.ExecuteJobAsync(Profile,Plan(),true,placement:new(StartFromMode.UserOrigin));Assert.Contains("G53 G0 X100 Y105 S0\n",t.Commands);
        await c.PositionAsync(Profile,new(20,30));Assert.Contains("$J=G21 G90 G53 X20 Y30 F600\n",t.Commands);Assert.Contains("M5 S0\n",t.Commands);
        await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);Assert.Null(c.UserOrigin);Assert.DoesNotContain(t.Commands,s=>s.Contains("G10")||s.Contains("G92"));
    }
    [Fact]public async Task AbsoluteJobUsesDrawingCoordinatesAndMissingSavedOriginWritesNoMotion()
    {
        var t=new Peer {MachineX=1,MachineY=1};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);
        await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteJobAsync(Profile,Plan(),true,placement:new(StartFromMode.UserOrigin)));Assert.DoesNotContain(t.Commands,s=>s.StartsWith("G53"));
        await c.ExecuteJobAsync(Profile,Plan(),true,placement:new(StartFromMode.AbsoluteCoordinates));Assert.Contains("G53 G0 X10 Y420 S0\n",t.Commands);
        var count=t.Commands.Count;await Assert.ThrowsAsync<InvalidDataException>(()=>c.PositionAsync(Profile,new(431,0)));Assert.DoesNotContain(t.Commands.Skip(count),s=>s.StartsWith("$J=")||s.StartsWith("M5"));
    }
    [Fact]public async Task AutomaticHomeWaitsOneSecondAndChecksCapability()
    {
        var t=new Peer();await using var c=await GrblMonitor.ConnectAsync(t);var timer=System.Diagnostics.Stopwatch.StartNew();var startup=c.HomeAfterConnectAsync(Profile);
        await Task.Delay(100);Assert.DoesNotContain("$H\n",t.Commands);await startup;Assert.True(timer.Elapsed>=TimeSpan.FromMilliseconds(950));Assert.True(c.HomingConfirmed);Assert.Single(t.Commands,x=>x=="$H\n");
    }
    [Fact]public async Task DisabledHomingStillLeavesConnectionUsableAndStartupCanBeCancelled()
    {
        var t=new Peer {Replacement="$22=0"};await using var c=await GrblMonitor.ConnectAsync(t);var result=await c.HomeAfterConnectAsync(Profile);Assert.Contains("skipped",result.Summary);Assert.DoesNotContain("$H\n",t.Commands);Assert.False(c.HomingConfirmed);Assert.Equal("Idle",(await c.QueryStatusAsync()).State);
        var before=t.Commands.Count;using var cancel=new CancellationTokenSource(50);await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>c.HomeAfterConnectAsync(Profile,cancel.Token));Assert.Equal(before,t.Commands.Count);
    }
    [Fact]public async Task DelayedAckReportsRunAndDoesNotMistakeStatusForAcknowledgement()
    {
        var t=new Peer {HoldFirstMotion=true,PendingMotionState="Run"};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);var job=c.ExecuteJobAsync(Profile,Plan(),true);await t.Motion.Task;
        await Task.Delay(1300);Assert.True(c.JobActive);Assert.Equal("Run",c.JobControllerState);Assert.Single(t.Commands,x=>x.StartsWith("G53"));t.Reply("ok");await job;Assert.False(c.JobActive);Assert.False(c.RequiresReconnect);await c.ExecuteManualAsync(Profile,new(GrblManualAction.JogX),true);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task MissingAckReleasesJobAndRequiresReconnectEvenIfIdleIsReported(bool silence)
    {
        var t=new Peer {HoldFirstMotion=true,SilenceAfterMotion=silence};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);var job=c.ExecuteJobAsync(Profile,Plan(),true);
        var error=await Assert.ThrowsAsync<IOException>(()=>job.WaitAsync(TimeSpan.FromSeconds(20)));Assert.Contains("unknown",error.Message);Assert.False(c.JobActive);Assert.False(c.JobPaused);Assert.True(c.RequiresReconnect);Assert.Single(t.Commands,x=>x.StartsWith("G53"));Assert.Single(t.Commands,x=>x=="\u0018");
    }
    [Fact]public async Task CompletionWaitsForIdleThenReleasesControlAndAbortAllowsFreshQueryOnlyReconnect()
    {
        var t=new Peer {CompletionRunningReports=2};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);await c.ExecuteJobAsync(Profile,Plan(),true);Assert.Equal(3,t.CompletionQueries);Assert.False(c.JobActive);await c.ExecuteManualAsync(Profile,new(GrblManualAction.JogX),true);
        var held=new Peer {HoldFirstMotion=true};await using var aborted=await GrblMonitor.ConnectAsync(held);await aborted.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);var job=aborted.ExecuteJobAsync(Profile,Plan(),true);await held.Motion.Task;await aborted.StopJobAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>job);Assert.False(aborted.JobActive);
        var fresh=new Peer();await using var reconnected=await GrblMonitor.ConnectAsync(fresh);Assert.Equal("Idle",(await reconnected.QueryStatusAsync()).State);await reconnected.ExecuteManualAsync(Profile,new(GrblManualAction.JogX),true);Assert.DoesNotContain("$H\n",fresh.Commands);Assert.False(reconnected.HomingConfirmed);
    }
    [Fact]public async Task AbortStartupAlarmCanIdentifyButJogRequiresHomeAndDoesNotUnlock()
    {
        var t=new Peer {ResetAlarmStartup=true};var journal=new ConnectionJournal();await using var c=await GrblMonitor.ConnectAsync(t,journal:journal);Assert.Equal("Alarm",(await c.QueryStatusAsync()).State);
        await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteManualAsync(Profile,new(GrblManualAction.JogX),true));await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);await c.ExecuteManualAsync(Profile,new(GrblManualAction.JogX),true);
        Assert.Contains(journal.Snapshot().Entries,e=>e.Direction=="RX"&&e.Text=="ALARM:3");Assert.DoesNotContain("$X\n",t.Commands);
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task CompletedJobsReturnLaserOffAndRepeatTheSameCoordinates(bool negative)
    {
        var t=new Peer {MachineX=negative?-100:100,MachineY=negative?-100:100};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);
        var before=t.Commands.Count;await c.ExecuteJobAsync(Profile,Plan(),true);var first=t.Commands.Skip(before).Where(x=>x.StartsWith("G53")).ToArray();before=t.Commands.Count;await c.ExecuteJobAsync(Profile,Plan(),true);Assert.Equal(first,t.Commands.Skip(before).Where(x=>x.StartsWith("G53")));
        Assert.Equal(negative?"G53 G0 X-100 Y-100 S0\n":"G53 G0 X100 Y100 S0\n",first[^1]);var returnIndex=t.Commands.FindLastIndex(x=>x==first[^1]);Assert.Equal("M9\n",t.Commands[returnIndex-1]);Assert.Equal("M5\n",t.Commands[returnIndex-2]);Assert.Equal("?",t.Commands[^1]);
    }
    [Fact]public async Task ReturnFailureDoesNotReportCompletionOrRetry()
    {
        var t=new Peer {RejectReturn=true};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);await Assert.ThrowsAsync<IOException>(()=>c.ExecuteJobAsync(Profile,Plan(),true));Assert.True(c.RequiresReconnect);Assert.False(c.JobActive);Assert.Single(t.Commands,x=>x=="G53 G0 X-59 Y-75 S0\n");Assert.Contains("\u0018",t.Commands);
    }
    [Theory][InlineData("MPos",1)][InlineData("WPos",0)]
    public async Task IdleWithoutConfirmedStartingPositionIsNotSuccess(string coordinates,double error)
    {
        var t=new Peer {FinalCoordinates=coordinates,FinalPositionError=error};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);var failure=await Assert.ThrowsAsync<IOException>(()=>c.ExecuteJobAsync(Profile,Plan(),true));Assert.Contains("return was not confirmed",failure.Message);Assert.True(c.RequiresReconnect);Assert.False(c.JobActive);
    }
    [Fact]public async Task UnreturnableStartRejectsAbsoluteJobBeforeOutput()
    {
        var t=new Peer {MachineX=500,MachineY=500};await using var c=await GrblMonitor.ConnectAsync(t);await c.ExecuteManualAsync(Profile,new(GrblManualAction.Home),true);await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteJobAsync(Profile,Plan(),true,placement:new(StartFromMode.AbsoluteCoordinates)));Assert.DoesNotContain(t.Commands,x=>x.StartsWith("G53"));
    }
    private sealed class Peer:IGrblTransport
    {
        readonly Channel<string> replies=Channel.CreateUnbounded<string>();public List<string> Commands {get;}=[];public bool HoldFirstMotion {get;init;}public bool RejectMotion {get;init;}public double MachineX {get;set;}=-59;public double MachineY {get;set;}=-75;public string? Replacement {get;init;}public TaskCompletionSource Motion {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public string FinalCoordinates {get;init;}="MPos"; public double FinalPositionError {get;init;} public bool RejectReturn {get;init;} public bool ResetAlarmStartup {get;init;} private bool homed; public bool SilenceAfterMotion {get;init;} public string PendingMotionState {get;init;}="Idle"; private bool waitingMotionAck; public int CompletionRunningReports {get;init;} public int CompletionQueries {get;private set;} private bool finishing;
        public void Reply(string line){if(line=="ok")waitingMotionAck=false;Push(line);}
        void Push(params string[] lines){foreach(var l in lines)replies.Writer.TryWrite(l);}
        public ValueTask WriteAsync(string text,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Commands.Add(text);
            if(text=="$I\n"){if(ResetAlarmStartup)Push("Grbl 1.1h reset","ALARM:3");Push("[VER:1.1h.2022071501:]","ok");}
            else if(text=="$$\n"){foreach(var s in new[]{"$13=0","$22=1","$30=1000","$31=0","$32=1","$130=430","$131=430"})Push(Replacement is not null&&s.Split('=')[0]==Replacement.Split('=')[0]?Replacement:s);Push("ok");}
            else if(text=="?"){if(!(Motion.Task.IsCompleted&&SilenceAfterMotion)){var state=ResetAlarmStartup&&!homed?"Alarm":waitingMotionAck?PendingMotionState:finishing&&++CompletionQueries<=CompletionRunningReports?"Run":"Idle";Push(FormattableString.Invariant($"<{state}|{(finishing?FinalCoordinates:"MPos")}:{MachineX+(finishing?FinalPositionError:0)},{MachineY},0|FS:0,0>"));}}
            else if(text is "!" or "~" or "\u0018"){}
            else if(RejectReturn&&finishing&&text.StartsWith("G53"))Push("error:20");
            else if(text.StartsWith("G53",StringComparison.Ordinal)&&!Motion.Task.IsCompleted){waitingMotionAck=HoldFirstMotion;Motion.TrySetResult();if(!HoldFirstMotion)Push(RejectMotion?"error:20":"ok");}
            else {if(text=="$H\n")homed=true;if(text=="M9\n"&&Motion.Task.IsCompleted)finishing=true;Push("ok");}return ValueTask.CompletedTask;
        }
        public ValueTask<string> ReadLineAsync(CancellationToken token)=>replies.Reader.ReadAsync(token);
        public ValueTask DisposeAsync(){replies.Writer.TryComplete();return ValueTask.CompletedTask;}
    }
}
