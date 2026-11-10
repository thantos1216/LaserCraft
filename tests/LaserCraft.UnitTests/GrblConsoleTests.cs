using System.Threading.Channels;
using LaserCraft.Devices;

namespace LaserCraft.UnitTests;

public sealed class GrblConsoleTests
{
    [Theory]
    [InlineData("")] [InlineData("   ")] [InlineData("G0\nM3")] [InlineData("G0\rM3")] [InlineData("\t$I")] [InlineData("é")] [InlineData("\u0018")] [InlineData("G0(comment!)")] [InlineData("G0;?")] [InlineData("~")]
    public void InvalidOrEmbeddedRealtimeInputNeverBecomesAWireCommand(string input) => Assert.Throws<InvalidDataException>(() => GrblConsoleCommand.Prepare(input, true));
    [Theory]
    [InlineData("$32=1")] [InlineData("M3 S10")] [InlineData("G0 X10")] [InlineData("$N0=G0X0")] [InlineData("$I=changed")]
    public void StateChangingCommandsRequireExplicitOptIn(string command)
    {
        Assert.Throws<InvalidDataException>(() => GrblConsoleCommand.Prepare(command, false));
        var prepared = GrblConsoleCommand.Prepare(command, true); Assert.False(prepared.IsQuery); Assert.Equal(command + "\n", prepared.Wire);
    }
    [Fact]
    public void QueryClassificationAndLineBudgetAreExact()
    {
        foreach (var command in new[] { "?", "$", "$$", "$I", "$G", "$#", "$N" }) Assert.True(GrblConsoleCommand.Prepare(command, false).IsQuery);
        Assert.Equal("?", GrblConsoleCommand.Prepare(" ? ", false).Wire);
        Assert.Equal(80, GrblConsoleCommand.Prepare(new string('A', 79), true).Wire.Length);
        Assert.Throws<InvalidDataException>(() => GrblConsoleCommand.Prepare(new string('A', 80), true));
    }
    [Fact]
    public async Task SimulatorSettingChangeCanBeReadBackWithoutChangingTheSavedProfile()
    {
        var transport = new SimulatedGrblTransport(); await using var client = await GrblMonitor.ConnectAsync(transport);
        await Assert.ThrowsAsync<InvalidDataException>(() => client.SubmitConsoleAsync("$32=0")); Assert.Single(transport.Commands);
        var write = await client.SubmitConsoleAsync("$32=0", true); Assert.Equal(GrblConsoleOutcome.Acknowledged, write.Outcome);
        var query = await client.SubmitConsoleAsync("$$"); Assert.Contains("$32=0", query.Lines);
        Assert.Equal(GrblConsoleOutcome.Rejected, (await client.SubmitConsoleAsync("G0 X1", true)).Outcome);
        Assert.Equal(GrblConsoleOutcome.Status, (await client.SubmitConsoleAsync("?")).Outcome);
        Assert.Equal(new[] { "$I\n", "$32=0\n", "$$\n", "G0 X1\n", "?" }, transport.Commands);
        Assert.Equal(0, transport.Simulation.Position.X);
    }
    [Theory]
    [InlineData("[MSG:hello]\n<Idle|FS:0,0>\nok", GrblConsoleOutcome.Acknowledged)]
    [InlineData("error:20", GrblConsoleOutcome.Rejected)]
    [InlineData("ALARM:1", GrblConsoleOutcome.Alarm)]
    public async Task TerminalRepliesAndPushMessagesArePreserved(string response, GrblConsoleOutcome outcome)
    {
        var t = new Replies(response.Split('\n')); await using var client = await GrblMonitor.ConnectAsync(t);
        var result = await client.SubmitConsoleAsync("M5", true); Assert.Equal(outcome, result.Outcome); Assert.Equal(response.Split('\n'), result.Lines);
    }
    [Fact]
    public async Task AckIsConsumedBeforeAnOverlappingStatusQueryCanWrite()
    {
        var t = new Replies([]); await using var client = await GrblMonitor.ConnectAsync(t);
        var command = client.SubmitConsoleAsync("$32=1", true); await t.Requested.Task; var status = client.QueryStatusAsync();
        Assert.Equal(new[] { "$I\n", "$32=1\n" }, t.Commands); t.Push("ok"); await command;
        Assert.Equal("Idle", (await status).State); Assert.Equal(new[] { "$I\n", "$32=1\n", "?" }, t.Commands);
    }
    [Fact]
    public async Task CancellationAfterWritePoisonsTheSessionAndNeverResends()
    {
        var t = new Replies([]); await using var client = await GrblMonitor.ConnectAsync(t); using var cancel = new CancellationTokenSource();
        var command = client.SubmitConsoleAsync("G4 P30", true, cancellationToken: cancel.Token); await t.Requested.Task; cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command); t.Push("ok");
        await Assert.ThrowsAsync<IOException>(() => client.QueryStatusAsync()); await Assert.ThrowsAsync<IOException>(() => client.SubmitConsoleAsync("$$"));
        Assert.Equal(new[] { "$I\n", "G4 P30\n" }, t.Commands);
    }
    [Fact]
    public async Task DisconnectCancelsTheConsoleAndQueuedQuery()
    {
        var t = new Replies([]); var client = await GrblMonitor.ConnectAsync(t); var command = client.SubmitConsoleAsync("$$"); await t.Requested.Task;
        var status = client.QueryStatusAsync(); await client.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => command); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => status); Assert.True(t.Disposed);
        Assert.Equal(new[] { "$I\n", "$$\n" }, t.Commands);
    }
    [Fact]
    public async Task TimeoutAndCallerPreCancellationHaveDistinctWireOutcomes()
    {
        var t = new Replies([]); await using var client = await GrblMonitor.ConnectAsync(t); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SubmitConsoleAsync("$$", cancellationToken: cancel.Token)); Assert.Single(t.Commands);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SubmitConsoleAsync("$$", timeoutSeconds: 1));
        await Assert.ThrowsAsync<IOException>(() => client.QuerySettingsAsync()); Assert.Equal(2, t.Commands.Count);
    }
    [Theory]
    [InlineData("Grbl 1.1h ['$' for help]")]
    [InlineData("oversize")]
    [InlineData("budget")]
    public async Task RestartAndResponseLimitsFailClosed(string response)
    {
        var lines = response == "budget" ? Enumerable.Repeat("[MSG:busy]", 256) : new[] { response == "oversize" ? new string('x', 2049) : response };
        var t = new Replies(lines); await using var client = await GrblMonitor.ConnectAsync(t);
        await Assert.ThrowsAsync(response.StartsWith("Grbl", StringComparison.Ordinal) ? typeof(IOException) : typeof(InvalidDataException), async () => await client.SubmitConsoleAsync("$G"));
        await Assert.ThrowsAsync<IOException>(() => client.QueryModalStateAsync()); Assert.Equal(2, t.Commands.Count);
    }
    private sealed class Replies(IEnumerable<string> response) : IGrblTransport
    {
        private readonly Channel<string> lines = Channel.CreateUnbounded<string>();
        public List<string> Commands { get; } = [];
        public bool Disposed { get; private set; }
        public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Push(string line) => lines.Writer.TryWrite(line);
        public ValueTask WriteAsync(string command, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Commands.Add(command);
            if (command == "$I\n") { Push("[VER:1.1h:test]"); Push("ok"); }
            else if (command == "?") Push("<Idle|MPos:0,0,0|FS:0,0>");
            else { foreach (var line in response) Push(line); Requested.TrySetResult(); }
            return ValueTask.CompletedTask;
        }
        public ValueTask<string> ReadLineAsync(CancellationToken token) => lines.Reader.ReadAsync(token);
        public ValueTask DisposeAsync() { Disposed = true; lines.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
