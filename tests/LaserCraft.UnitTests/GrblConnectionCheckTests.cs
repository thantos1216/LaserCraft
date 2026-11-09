using System.Threading.Channels;
using LaserCraft.Devices;
using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class GrblConnectionCheckTests
{
    [Fact]
    public void ModalReportPreservesUnknownWordsAndDistinguishesUnitsFromReportingUnits()
    {
        var modal = GrblModalState.Parse("[GC:G0 G54 G17 G21 G90 G94 M5 M9 T0 S0.0 F500.0 G99]");
        Assert.Equal("G21", modal.Units); Assert.Equal("M5", modal.Spindle); Assert.Contains("G99", modal.Words); Assert.Contains("S0.0", modal.Raw);
        var report = GrblConnectionCheck.Compare(DeviceProfile.Simulator(), [GrblSetting.Parse("$13=1")], modal, GrblStatus.Parse("<Idle|FS:0,0>"));
        Assert.Equal(CheckFinding.Match, report.Single(c => c.Name == "G-code units").Finding);
        Assert.Equal(CheckFinding.Attention, report.Single(c => c.Name == "Status reporting in millimetres").Finding);
        Assert.Equal(CheckFinding.Unknown, report.Single(c => c.Name == "S maximum").Finding);
    }
    [Theory]
    [InlineData("[GC:]")] [InlineData("[GC:G20 G21]")] [InlineData("[GC:M3 M5]")]
    [InlineData("[GC:G90 G91]")] [InlineData("[GC:G93 G94]")] [InlineData("[GC:G21 G21]")]
    [InlineData("[GC:S0 S1]")] [InlineData("[GC:G21\nM5]")] [InlineData("[GC:G21")] [InlineData("[GC:FNaN]")]
    public void MalformedModalReportsAreRejected(string line) => Assert.Throws<InvalidDataException>(() => GrblModalState.Parse(line));
    [Fact]
    public async Task ConnectionCheckUsesOnlyReadOnlyQueriesAndNeverEstablishesHoming()
    {
        var profile = DeviceProfile.Simulator() with { BedWidth = 430, BedHeight = 430 }; var transport = new SimulatedGrblTransport(profile);
        await using var monitor = await GrblMonitor.ConnectAsync(transport);
        var settings = await monitor.QuerySettingsAsync(); var modal = await monitor.QueryModalStateAsync(); var status = await monitor.QueryStatusAsync();
        var report = GrblConnectionCheck.Compare(profile, settings, modal, status);
        Assert.Equal(new[] { "$I\n", "$$\n", "$G\n", "?" }, transport.Commands);
        Assert.All(report.Where(c => c.Name != "Position / homing"), c => Assert.Equal(CheckFinding.Match, c.Finding));
        Assert.Equal(CheckFinding.Unknown, report.Single(c => c.Name == "Position / homing").Finding);
    }
    [Fact]
    public void MismatchesMissingFactsAndNonIdleStatesAreNotReportedAsMatches()
    {
        var settings = new[] { "$30=255", "$32=0", "$13=0", "$130=400", "$131=garbled" }.Select(GrblSetting.Parse).ToArray();
        var checks = GrblConnectionCheck.Compare(DeviceProfile.Simulator() with { BedWidth = 430 }, settings, GrblModalState.Parse("[GC:G20 G91 G93 M3]"), GrblStatus.Parse("<Alarm|FS:0,50>"));
        foreach (var name in new[] { "S maximum", "Laser mode", "X travel vs bed", "G-code units", "Distance mode", "Feed mode", "Spindle mode", "Controller state", "Reported spindle/S" })
            Assert.Equal(CheckFinding.Attention, checks.Single(c => c.Name == name).Finding);
        Assert.Equal(CheckFinding.Unknown, checks.Single(c => c.Name == "Y travel vs bed").Finding);
        Assert.Throws<InvalidDataException>(() => GrblConnectionCheck.Compare(DeviceProfile.Simulator(), [settings[0], settings[0]], GrblModalState.Parse("[GC:G21]"), GrblStatus.Parse("<Idle>")));
    }
    [Theory]
    [InlineData("ok", typeof(InvalidDataException))]
    [InlineData("[GC:G21]\n[GC:G21]\nok", typeof(InvalidDataException))]
    [InlineData("[GC:G20 G21]\nok", typeof(InvalidDataException))]
    [InlineData("error:8", typeof(IOException))]
    [InlineData("ALARM:1", typeof(IOException))]
    public async Task ModalQueryRequiresOneValidReportAndAcknowledgement(string response, Type exception)
    {
        var t = new Replies(response.Split('\n')); await using var monitor = await GrblMonitor.ConnectAsync(t);
        await Assert.ThrowsAsync(exception, async () => await monitor.QueryModalStateAsync()); Assert.Equal(new[] { "$I\n", "$G\n" }, t.Commands);
    }
    [Fact]
    public async Task DisconnectCancelsModalReadAndQueuedStatusWithoutSendingStatus()
    {
        var t = new Replies([]); var monitor = await GrblMonitor.ConnectAsync(t); var modal = monitor.QueryModalStateAsync();
        await t.Requested.Task.WaitAsync(TimeSpan.FromSeconds(2)); var status = monitor.QueryStatusAsync();
        await monitor.DisposeAsync(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => modal); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => status);
        Assert.Equal(new[] { "$I\n", "$G\n" }, t.Commands); Assert.True(t.Disposed);
    }
    [Fact]
    public async Task ModalQueryDoesNotSucceedBeforeAckAndHonorsCallerCancellation()
    {
        var t = new Replies(["[GC:G21]"]); await using var monitor = await GrblMonitor.ConnectAsync(t); using var cancel = new CancellationTokenSource();
        var query = monitor.QueryModalStateAsync(cancel.Token); await t.Requested.Task; Assert.False(query.IsCompleted); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query);
    }
    private sealed class Replies(IEnumerable<string> response) : IGrblTransport
    {
        private readonly Channel<string> lines = Channel.CreateUnbounded<string>();
        public List<string> Commands { get; } = [];
        public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Disposed { get; private set; }
        public ValueTask WriteAsync(string command, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Commands.Add(command);
            if (command == "$I\n") { lines.Writer.TryWrite("[VER:1.1h:test]"); lines.Writer.TryWrite("ok"); }
            else { foreach (var line in response) lines.Writer.TryWrite(line); Requested.TrySetResult(); }
            return ValueTask.CompletedTask;
        }
        public ValueTask<string> ReadLineAsync(CancellationToken token) => lines.Reader.ReadAsync(token);
        public ValueTask DisposeAsync() { Disposed = true; lines.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
