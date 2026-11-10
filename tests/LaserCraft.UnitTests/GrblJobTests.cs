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
    private sealed class Peer:IGrblTransport
    {
        readonly Channel<string> replies=Channel.CreateUnbounded<string>();public List<string> Commands {get;}=[];public bool HoldFirstMotion {get;init;}public bool RejectMotion {get;init;}public double MachineX {get;init;}=-59;public double MachineY {get;init;}=-75;public string? Replacement {get;init;}public TaskCompletionSource Motion {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Reply(string line)=>Push(line);
        void Push(params string[] lines){foreach(var l in lines)replies.Writer.TryWrite(l);}
        public ValueTask WriteAsync(string text,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Commands.Add(text);
            if(text=="$I\n")Push("[VER:1.1h.2022071501:]","ok");
            else if(text=="$$\n"){foreach(var s in new[]{"$13=0","$22=1","$30=1000","$31=0","$32=1","$130=430","$131=430"})Push(Replacement is not null&&s.Split('=')[0]==Replacement.Split('=')[0]?Replacement:s);Push("ok");}
            else if(text=="?")Push(FormattableString.Invariant($"<Idle|MPos:{MachineX},{MachineY},0|FS:0,0>"));
            else if(text is "!" or "~" or "\u0018"){}
            else if(text.StartsWith("G53",StringComparison.Ordinal)&&!Motion.Task.IsCompleted){Motion.TrySetResult();if(!HoldFirstMotion)Push(RejectMotion?"error:20":"ok");}
            else Push("ok");return ValueTask.CompletedTask;
        }
        public ValueTask<string> ReadLineAsync(CancellationToken token)=>replies.Reader.ReadAsync(token);
        public ValueTask DisposeAsync(){replies.Writer.TryComplete();return ValueTask.CompletedTask;}
    }
}
