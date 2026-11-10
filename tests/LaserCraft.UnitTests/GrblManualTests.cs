using System.Threading.Channels;
using LaserCraft.Domain;
using LaserCraft.Devices;
namespace LaserCraft.UnitTests;
public sealed class GrblManualTests
{
    private static DeviceProfile Profile => DeviceProfile.Simulator() with { Connection = ConnectionKind.Serial, SerialPort = "COM9", MaxPower = 1000 };
    [Theory]
    [InlineData(GrblManualAction.JogX, false, false)] [InlineData(GrblManualAction.Pulse, true, false)]
    public async Task PermissionsAreRequiredBeforeAnyAdditionalWrites(GrblManualAction action, bool motion, bool fire)
    {
        var t = new Peer(); await using var c = await GrblMonitor.ConnectAsync(t);
        await Assert.ThrowsAsync<InvalidDataException>(() => c.ExecuteManualAsync(Profile, new(action), motion, fire)); Assert.Single(t.Commands);
    }
    [Theory] [InlineData(0)] [InlineData(10000.001)] [InlineData(-10001)] [InlineData(double.NaN)] [InlineData(double.PositiveInfinity)]
    public void InvalidJogDistanceIsRejected(double distance) => Assert.Throws<InvalidDataException>(() => new GrblManualRequest(GrblManualAction.JogX, distance).Validate());
    [Theory] [InlineData(0,100)] [InlineData(5.001,100)] [InlineData(1,0)] [InlineData(1,501)] [InlineData(double.NaN,100)]
    public void InvalidPulseIsRejected(double percent, int ms) => Assert.Throws<InvalidDataException>(() => new GrblManualRequest(GrblManualAction.Pulse, PowerPercent:percent, Milliseconds:ms).Validate());
    [Fact] public async Task IdleJogDisablesLaserThenUsesNonModalMillimetreIncrementalCommand()
    {
        var t = new Peer(); await using var c = await GrblMonitor.ConnectAsync(t);
        var result = await c.ExecuteManualAsync(Profile, new(GrblManualAction.JogY, -1.25, 600), true);
        Assert.Equal(new[] { "$I\n", "?", "M5 S0\n", "$J=G21 G91 Y-1.25 F600\n" }, t.Commands);
        Assert.Contains("not completed", result.Summary); Assert.False(c.RequiresReconnect);
    }
    [Theory] [InlineData("Run")] [InlineData("Jog")] [InlineData("Alarm")] [InlineData("Hold:0")]
    public async Task BusyControllerDoesNotReceiveMotionOrFire(string state)
    {
        var t = new Peer { State = state }; await using var c = await GrblMonitor.ConnectAsync(t);
        await Assert.ThrowsAsync<InvalidDataException>(() => c.ExecuteManualAsync(Profile, new(GrblManualAction.JogX), true)); Assert.Equal(new[] { "$I\n", "?" }, t.Commands);
    }
    [Fact] public async Task PulseWritesDwellAndOffInSameBoundedWriteBeforeWaitingForReplies()
    {
        var t = new Peer { HoldBatch = true }; await using var c = await GrblMonitor.ConnectAsync(t);
        var pulse = c.ExecuteManualAsync(Profile, new(GrblManualAction.Pulse, PowerPercent:2.5, Milliseconds:250), true, true); await t.BatchWritten.Task;
        Assert.Equal("G1 G94 F60 M3 S25\nG4 P0.25\nM5 S0\n", t.Commands[^1]); Assert.False(pulse.IsCompleted);
        var other = c.SubmitConsoleAsync("$$"); Assert.Equal(5, t.Commands.Count);
        t.Push("ok", "[MSG:pulse]", "ok", "ok"); var result = await pulse; await other;
        Assert.Contains("M5", result.Summary); Assert.False(c.RequiresReconnect); Assert.Equal("$$\n", t.Commands[^1]);
    }
    [Theory] [InlineData("$30=999", "$31=0", "$32=1")] [InlineData("$30=1000", "$31=1", "$32=1")] [InlineData("$30=1000", "$31=0", "$32=0")] [InlineData("$30=1000", "$31=0", "$32=NaN")] [InlineData("$30=1000", "$31=0", "$31=0")]
    public async Task UnknownOrMismatchedSettingsNeverPermitFire(string a,string b,string d)
    {
        var t = new Peer { Settings = [a,b,d] }; await using var c = await GrblMonitor.ConnectAsync(t);
        await Assert.ThrowsAsync<InvalidDataException>(() => c.ExecuteManualAsync(Profile, new(GrblManualAction.Pulse), true,true));
        Assert.DoesNotContain(t.Commands, s => s.Contains("M3")); Assert.False(c.RequiresReconnect);
    }
    [Fact] public async Task CancelAndOffHaveDistinctProtocolsAndDoNotRequireIdle()
    {
        var t = new Peer { State = "Jog" }; await using var c = await GrblMonitor.ConnectAsync(t);
        await c.ExecuteManualAsync(Profile, new(GrblManualAction.CancelJog), true); await c.ExecuteManualAsync(Profile, new(GrblManualAction.LaserOff), true);
        Assert.Equal(new[] { "$I\n", "\u0085", "M5 S0\n" }, t.Commands);
    }
    [Fact] public async Task InterruptedPulsePoisonsSessionWithoutRetryOrClaimingOff()
    {
        var t = new Peer { HoldBatch = true }; await using var c = await GrblMonitor.ConnectAsync(t); using var stop = new CancellationTokenSource();
        var pulse = c.ExecuteManualAsync(Profile, new(GrblManualAction.Pulse),true,true,stop.Token); await t.BatchWritten.Task; stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pulse); Assert.True(c.RequiresReconnect);
        await Assert.ThrowsAsync<IOException>(()=>c.SubmitConsoleAsync("M5",true)); Assert.Equal(5,t.Commands.Count);
    }
    [Fact] public async Task BatchErrorIsDrainedButRequiresReconnect()
    {
        var t = new Peer { BatchReplies = ["error:20","ok","ok"] }; await using var c = await GrblMonitor.ConnectAsync(t);
        await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteManualAsync(Profile,new(GrblManualAction.Pulse),true,true)); Assert.True(c.RequiresReconnect);
    }
    [Fact] public async Task FirmwareFamilyAndIdentityAreQualifiedAndPreCancellationWritesNothing()
    {
        var t = new Peer(); await using var c = await GrblMonitor.ConnectAsync(t);
        await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteManualAsync(Profile with {Controller=ControllerFamily.FluidNc},new(GrblManualAction.JogX),true));
        using var stop=new CancellationTokenSource();stop.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>c.ExecuteManualAsync(Profile,new(GrblManualAction.JogX),true,cancellationToken:stop.Token)); Assert.Single(t.Commands);
        var old = new Peer { Identity="[VER:1.0c:test]" };await using var legacy=await GrblMonitor.ConnectAsync(old);await Assert.ThrowsAsync<InvalidDataException>(()=>legacy.ExecuteManualAsync(Profile,new(GrblManualAction.JogX),true));Assert.Single(old.Commands);
    }
    [Theory] [InlineData("ALARM:1")] [InlineData("Grbl 1.1h reset")]
    public async Task PulseAlarmOrRestartRequiresReconnect(string reply)
    {
        var t = new Peer { BatchReplies = [reply] }; await using var c = await GrblMonitor.ConnectAsync(t);
        await Assert.ThrowsAsync<IOException>(() => c.ExecuteManualAsync(Profile, new(GrblManualAction.Pulse), true, true)); Assert.True(c.RequiresReconnect);
    }
    [Fact] public async Task DisconnectDuringPulseCancelsQueuedOperations()
    {
        var t = new Peer { HoldBatch = true }; var c = await GrblMonitor.ConnectAsync(t);
        var pulse = c.ExecuteManualAsync(Profile, new(GrblManualAction.Pulse), true, true); await t.BatchWritten.Task;
        var queued = c.QueryStatusAsync(); await c.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pulse); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued);
        Assert.EndsWith("M5 S0\n", t.Commands[^1]); Assert.Equal(5, t.Commands.Count);
    }
    [Theory] [InlineData(0)] [InlineData(59.9)] [InlineData(3000.1)] [InlineData(double.NaN)]
    public void PhysicalFeedLimitsAreValidated(double feed) => Assert.Throws<InvalidDataException>(() => new GrblManualRequest(GrblManualAction.JogX, Feed: feed).Validate());
    [Theory] [InlineData(GrblManualAction.CutLineX,5,"X5")] [InlineData(GrblManualAction.CutLineY,-5,"Y-5")]
    public async Task LineTestUsesObservedDecimalSettingsAndEndsOffAbsoluteIdle(GrblManualAction action,double distance,string axis)
    {
        var t=new Peer {Settings=["$30=1000.000","$31=0.000","$32=1"],BatchReplies=["ok","ok","ok","ok"]};await using var c=await GrblMonitor.ConnectAsync(t);
        var result=await c.ExecuteManualAsync(Profile,new(action,distance,600,10,ClearPathConfirmed:true),true,true);
        Assert.Contains("G21 G91 G94 M4 S0\nG1 "+axis+" F600 S100\nM5 S0\nG90 G0\n",t.Commands);Assert.Equal("?",t.Commands[^1]);Assert.Contains("reported Idle",result.Summary);Assert.False(c.RequiresReconnect);
    }
    [Theory] [InlineData("[GC:G20 G90 G94 M5]")] [InlineData("[GC:G21 G91 G94 M5]")] [InlineData("[GC:G21 G90 G93 M5]")] [InlineData("")]
    public async Task MissingOrUnsuitableModesDoNotSendCut(string modal)
    {
        var t=new Peer {ModalReport=modal};await using var c=await GrblMonitor.ConnectAsync(t);await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteManualAsync(Profile,new(GrblManualAction.CutLineX,ClearPathConfirmed:true),true,true));Assert.DoesNotContain(t.Commands,x=>x.StartsWith("G21 G91",StringComparison.Ordinal));
    }
    [Fact] public async Task LineNeedsClearanceAndFiringPermissionBeforeWrites()
    {
        var t=new Peer();await using var c=await GrblMonitor.ConnectAsync(t);await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteManualAsync(Profile,new(GrblManualAction.CutLineX),true,true));await Assert.ThrowsAsync<InvalidDataException>(()=>c.ExecuteManualAsync(Profile,new(GrblManualAction.CutLineX,ClearPathConfirmed:true),true,false));Assert.Single(t.Commands);
    }
    [Theory] [InlineData(0,600,10)] [InlineData(20.1,600,10)] [InlineData(5,59,10)] [InlineData(5,3001,10)] [InlineData(5,600,101)] [InlineData(5,600,0)]
    public void LineBudgetsAreValidated(double distance,double feed,double power)=>Assert.Throws<InvalidDataException>(()=>new GrblManualRequest(GrblManualAction.CutLineX,distance,feed,power).Validate());
    [Fact] public async Task NonIdleCompletionIsNotReportedAsASuccessfulCut()
    {
        var t=new Peer {BatchReplies=["ok","ok","ok","ok"],CompletionState="Run"};await using var c=await GrblMonitor.ConnectAsync(t);await Assert.ThrowsAsync<IOException>(()=>c.ExecuteManualAsync(Profile,new(GrblManualAction.CutLineX,ClearPathConfirmed:true),true,true));Assert.True(c.RequiresReconnect);
    }
    [Fact] public async Task InterruptedLineKeepsOffInAttemptedBatchAndRequiresReconnect()
    {
        var t=new Peer {HoldBatch=true};await using var c=await GrblMonitor.ConnectAsync(t);using var cancel=new CancellationTokenSource();var task=c.ExecuteManualAsync(Profile,new(GrblManualAction.CutLineX,ClearPathConfirmed:true),true,true,cancel.Token);await t.BatchWritten.Task;cancel.Cancel();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>task);Assert.Contains("M5 S0\nG90 G0\n",t.Commands[^1]);Assert.True(c.RequiresReconnect);
    }
    private sealed class Peer : IGrblTransport
    {
        readonly Channel<string> replies=Channel.CreateUnbounded<string>();
        public List<string> Commands {get;}=[];
        public string State {get;init;}="Idle";
        public string CompletionState {get;init;}="Idle";
        public string Identity {get;init;}="[VER:1.1h:test]";
        public string ModalReport {get;init;}="[GC:G21 G90 G94 M5]";
        public string[] Settings {get;init;}=["$30=1000","$31=0","$32=1"];
        public string[] BatchReplies {get;init;}=["ok","ok","ok"];
        public bool HoldBatch {get;init;}
        public TaskCompletionSource BatchWritten {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Push(params string[] lines) {foreach(var line in lines)replies.Writer.TryWrite(line);}
        public ValueTask WriteAsync(string command,CancellationToken token)
        {
            token.ThrowIfCancellationRequested();Commands.Add(command);
            if(command=="$I\n")Push(Identity,"ok");
            else if(command=="?")Push("<"+(BatchWritten.Task.IsCompleted?CompletionState:State)+"|MPos:0,0,0|FS:0,0>");
            else if(command=="$$\n") {Push(Settings);Push("ok");}
            else if(command=="$G\n") {if(ModalReport.Length>0)Push(ModalReport);Push("ok");}
            else if(command.StartsWith("G1 G94",StringComparison.Ordinal)||command.StartsWith("G21 G91",StringComparison.Ordinal)) {BatchWritten.TrySetResult();if(!HoldBatch)Push(BatchReplies);}
            else if(command!="\u0085")Push("ok");
            return ValueTask.CompletedTask;
        }
        public ValueTask<string> ReadLineAsync(CancellationToken token)=>replies.Reader.ReadAsync(token);
        public ValueTask DisposeAsync(){replies.Writer.TryComplete();return ValueTask.CompletedTask;}
    }
}
