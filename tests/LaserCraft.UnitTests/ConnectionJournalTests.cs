using System.Threading.Channels;
using LaserCraft.Devices;

namespace LaserCraft.UnitTests;

public sealed class ConnectionJournalTests
{
    [Fact]
    public void JournalBoundsStorageAndExportsAnIndependentEscapedSnapshot()
    {
        var journal = new ConnectionJournal(); journal.Add("TX", "$I\n\\literal\r\t\u0018");
        var snapshot = journal.Snapshot(); var text = snapshot.Format();
        Assert.Contains("$I\\n\\\\literal\\r\\t\\u0018", text); Assert.Contains("attempted write", text);
        Assert.Equal(TimeSpan.Zero, snapshot.Entries[0].Time.Offset);
        for (int i = 0; i < 600; i++) journal.Add("RX", new string('a', 5000));
        var bounded = journal.Snapshot(); Assert.Equal(ConnectionJournal.Capacity, bounded.Entries.Count); Assert.Equal(89, bounded.Dropped);
        Assert.All(bounded.Entries, e => Assert.Equal(ConnectionJournal.EntryLimit + " [truncated]".Length, e.Text.Length));
        Assert.Single(snapshot.Entries); Assert.Equal(0, snapshot.Dropped);
        Assert.Throws<ArgumentException>(() => journal.Add("bad", "ignored"));
    }
    [Fact]
    public async Task InstrumentedMonitorPreservesSimulatorOwnershipAndExactQuerySequence()
    {
        var journal = new ConnectionJournal(); var transport = new SimulatedGrblTransport();
        var monitor = await GrblMonitor.ConnectAsync(transport, journal: journal);
        Assert.Same(transport.Simulation, monitor.Simulation); await monitor.QueryStatusAsync(); await monitor.QuerySettingsAsync(); await monitor.QueryModalStateAsync(); await monitor.DisposeAsync();
        Assert.Equal(new[] { "$I\n", "?", "$$\n", "$G\n" }, transport.Commands);
        var snapshot = journal.Snapshot(); Assert.Equal(transport.Commands, snapshot.Entries.Where(e => e.Direction == "TX").Select(e => e.Text));
        Assert.Contains(snapshot.Entries, e => e.Direction == "RX" && e.Text.StartsWith("[VER:"));
        Assert.Equal("Transport closed.", snapshot.Entries[^1].Text);
    }
    [Theory]
    [InlineData("error:8")]
    [InlineData("ALARM:1")]
    public async Task FailedHandshakeRetainsRawResponseAndClosesTransport(string reply)
    {
        var journal = new ConnectionJournal(); var t = new FaultTransport(reply);
        await Assert.ThrowsAsync<IOException>(() => GrblMonitor.ConnectAsync(t, journal: journal));
        Assert.True(t.Disposed); Assert.Contains(journal.Snapshot().Entries, e => e.Direction == "RX" && e.Text == reply);
    }
    [Fact]
    public async Task FailedWriteRetainsAttemptAndOriginalFailure()
    {
        var journal = new ConnectionJournal(); var t = new FaultTransport("") { FailWrite = true };
        var ex = await Assert.ThrowsAsync<IOException>(() => GrblMonitor.ConnectAsync(t, journal: journal)); Assert.Equal("test write failure", ex.Message);
        Assert.True(t.Disposed); var entries = journal.Snapshot().Entries;
        Assert.Equal("TX", entries[0].Direction); Assert.Equal("$I\n", entries[0].Text); Assert.Contains(entries, e => e.Direction == "ERROR" && e.Text.Contains("test write failure"));
    }
    [Fact]
    public async Task StatusAlarmFailsImmediatelyAndPreservesSourceLine()
    {
        var journal = new ConnectionJournal(); var t = new FaultTransport("ALARM:2") { Identify = true };
        await using var monitor = await GrblMonitor.ConnectAsync(t, journal: journal);
        var ex = await Assert.ThrowsAsync<IOException>(() => monitor.QueryStatusAsync()); Assert.Equal("ALARM:2", ex.Message);
        Assert.Contains(journal.Snapshot().Entries, e => e.Direction == "RX" && e.Text == "ALARM:2");
    }
    [Fact]
    public async Task DisconnectLogsCancelledReadAndClosesOnce()
    {
        var journal = new ConnectionJournal(); var t = new FaultTransport(null) { Identify = true };
        var monitor = await GrblMonitor.ConnectAsync(t, journal: journal); var query = monitor.QueryStatusAsync();
        await t.Requested.Task.WaitAsync(TimeSpan.FromSeconds(2)); await monitor.DisposeAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query); await monitor.DisposeAsync();
        Assert.Equal(1, journal.Snapshot().Entries.Count(e => e.Text == "Transport closed."));
        Assert.Contains(journal.Snapshot().Entries, e => e.Direction == "ERROR" && e.Text.StartsWith("Read:"));
    }
    private sealed class FaultTransport(string? reply) : IGrblTransport
    {
        private readonly Channel<string> lines = Channel.CreateUnbounded<string>();
        public bool FailWrite { get; init; }
        public bool Identify { get; init; }
        public bool Disposed { get; private set; }
        public TaskCompletionSource Requested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask WriteAsync(string command, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); if (FailWrite) throw new IOException("test write failure");
            if (Identify && command == "$I\n") { lines.Writer.TryWrite("[VER:1.1h:test]"); lines.Writer.TryWrite("ok"); }
            else { if (reply is not null) lines.Writer.TryWrite(reply); Requested.TrySetResult(); }
            return ValueTask.CompletedTask;
        }
        public ValueTask<string> ReadLineAsync(CancellationToken token) => lines.Reader.ReadAsync(token);
        public ValueTask DisposeAsync() { Disposed = true; lines.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
