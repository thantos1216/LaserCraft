using LaserCraft.Devices;
using LaserCraft.Domain;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Channels;

namespace LaserCraft.UnitTests;

public sealed class GrblDiagnosticsTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(10001)]
    public void InvalidSerialStartupDelaysAreRejected(int delay) => Assert.Throws<InvalidDataException>(() => (DeviceProfile.Simulator() with { SerialStartupDelayMs = delay }).Validate());
    [Fact]
    public void LegacyProfilesGetTheDefaultDelayAndExplicitDelayRoundTrips()
    {
        var p = DeviceProfile.Simulator() with { SerialStartupDelayMs = 3500 }; var path = Path.GetTempFileName();
        try
        {
            ProfileStore.Save(path, [p]); Assert.Equal(3500, Assert.Single(ProfileStore.Load(path)).SerialStartupDelayMs);
            var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path))!; json[0]!.AsObject().Remove("SerialStartupDelayMs"); File.WriteAllText(path, json.ToJsonString());
            Assert.Equal(2000, Assert.Single(ProfileStore.Load(path)).SerialStartupDelayMs);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void OptionalTelemetryIsParsedWithoutInventingMissingValues()
    {
        var s = GrblStatus.Parse("<Hold:0|WPos:-1.5,2,3,4|FS:1200.5,400|Bf:15,127|Pn:XP|A:FM|Future:abc>");
        Assert.Equal("Hold:0", s.State); Assert.Equal(-1.5, s.X); Assert.Equal(1200.5, s.Feed); Assert.Equal(400, s.Spindle);
        Assert.Equal(15, s.PlannerAvailable); Assert.Equal(127, s.ReceiveAvailable); Assert.Equal("XP", s.Pins); Assert.Equal("FM", s.Accessories); Assert.Contains("Future:abc", s.Raw);
        var missing = GrblStatus.Parse("<Alarm|F:0>"); Assert.Equal(0, missing.Feed); Assert.Null(missing.X); Assert.Null(missing.Spindle); Assert.Null(missing.Pins); Assert.Null(missing.PlannerAvailable);
    }
    [Theory]
    [InlineData("<Idle|FS:NaN,0>")]
    [InlineData("<Idle|FS:0>")]
    [InlineData("<Idle|Bf:1.5,128>")]
    [InlineData("<Idle|Bf:-1,128>")]
    [InlineData("<Idle|F:1|F:2>")]
    public void InvalidKnownTelemetryFailsExplicitly(string frame) => Assert.Throws<InvalidDataException>(() => GrblStatus.Parse(frame));
    [Fact]
    public async Task SettingsSimulatorUsesOnlyTheReadOnlyCommandAndRetainsSourceValues()
    {
        var transport = new SimulatedGrblTransport(); await using var m = await GrblMonitor.ConnectAsync(transport);
        var settings = await m.QuerySettingsAsync(); Assert.Equal("1000", settings.Single(s => s.Number == 30).Value); Assert.Equal("1", settings.Single(s => s.Number == 32).Value);
        Assert.Equal(new[] { "$I\n", "$$\n" }, transport.Commands);
        var unknown = GrblSetting.Parse("$999=01.2500"); Assert.Equal("01.2500", unknown.Value); Assert.Equal("$999=01.2500", unknown.Raw);
    }
    [Theory]
    [InlineData("ok", typeof(InvalidDataException))]
    [InlineData("$30=1000\n$30=255\nok", typeof(InvalidDataException))]
    [InlineData("$30=\nok", typeof(InvalidDataException))]
    [InlineData("error:8", typeof(IOException))]
    [InlineData("ALARM:1", typeof(IOException))]
    public async Task IncompleteMalformedOrRejectedSettingsAreNotPresentedAsSuccess(string response, Type expected)
    {
        var t = new Replies(response.Split('\n')); await using var m = await GrblMonitor.ConnectAsync(t);
        await Assert.ThrowsAsync(expected, async () => await m.QuerySettingsAsync()); Assert.Equal(new[] { "$I\n", "$$\n" }, t.Commands);
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task DisconnectCancelsActiveAndQueuedQueriesWithoutSendingTheQueuedCommand(bool settingsFirst)
    {
        for (int i = 0; i < 16; i++)
        {
            var t = new Replies([]); var m = await GrblMonitor.ConnectAsync(t);
            Task active = settingsFirst ? m.QuerySettingsAsync() : m.QueryStatusAsync();
            if (settingsFirst) await t.SettingsSent.Task.WaitAsync(TimeSpan.FromSeconds(2)); else Assert.Contains("?", t.Commands);
            Task queued = settingsFirst ? m.QueryStatusAsync() : m.QuerySettingsAsync();
            await m.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => active); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => queued); Assert.True(t.Disposed); Assert.DoesNotContain(settingsFirst ? "?" : "$$\n", t.Commands);
        }
    }
    [Fact]
    public async Task NewQueriesAfterDisposalFailWithoutSendingAnything()
    {
        var t = new SimulatedGrblTransport(); var m = await GrblMonitor.ConnectAsync(t); await m.DisposeAsync();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => m.QueryStatusAsync()); await Assert.ThrowsAsync<ObjectDisposedException>(() => m.QuerySettingsAsync());
        Assert.Equal(new[] { "$I\n" }, t.Commands);
    }
    [Fact]
    public async Task ResponseBudgetRejectsUnterminatedSettings()
    {
        var t = new Replies(Enumerable.Range(0, 256).Select(i => "$" + i + "=1").ToArray()); await using var m = await GrblMonitor.ConnectAsync(t);
        await Assert.ThrowsAsync<InvalidDataException>(() => m.QuerySettingsAsync());
    }
    [Fact]
    public async Task ActualTcpTransportReadsFragmentedSettingsAndSubsequentStatus()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8)); var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            var server = Task.Run(async () =>
            {
                using var peer = await listener.AcceptTcpClientAsync(timeout.Token); var stream = peer.GetStream();
                async Task Expect(string command) { var bytes = new byte[command.Length]; await stream.ReadExactlyAsync(bytes, timeout.Token); Assert.Equal(command, Encoding.ASCII.GetString(bytes)); }
                async Task Reply(string value) { foreach (var b in Encoding.ASCII.GetBytes(value)) await stream.WriteAsync(new byte[] { b }, timeout.Token); }
                await Expect("$I\n"); await Reply("[VER:1.1h:diagnostic-loopback]\r\nok\r\n");
                await Expect("$$\n"); await Reply("[MSG:Settings]\r\n$30=1000.000\r\n$32=1\r\nok\r\n");
                await Expect("?"); await Reply("<Idle|MPos:1,2,3|FS:0,0|Bf:15,128>\r\n");
            });
            var p = DeviceProfile.Simulator() with { Connection = ConnectionKind.Tcp, Host = "127.0.0.1", Port = ((IPEndPoint)listener.LocalEndpoint).Port };
            await using var m = await GrblMonitor.ConnectAsync(await TransportFactory.OpenAsync(p, timeout.Token), timeout.Token);
            Assert.Equal("1000.000", (await m.QuerySettingsAsync(timeout.Token)).Single(s => s.Number == 30).Value);
            Assert.Equal(128, (await m.QueryStatusAsync(timeout.Token)).ReceiveAvailable); await server;
        }
        finally { listener.Stop(); }
    }
    private sealed class Replies(string[] settings) : IGrblTransport
    {
        private readonly Channel<string> lines = Channel.CreateUnbounded<string>();
        public List<string> Commands { get; } = [];
        public bool Disposed { get; private set; }
        public TaskCompletionSource SettingsSent { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask WriteAsync(string command, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); Commands.Add(command);
            if (command == "$I\n") { lines.Writer.TryWrite("[VER:1.1h:test]"); lines.Writer.TryWrite("ok"); }
            else if (command == "$$\n") { foreach (var s in settings) lines.Writer.TryWrite(s); SettingsSent.TrySetResult(); }
            return ValueTask.CompletedTask;
        }
        public ValueTask<string> ReadLineAsync(CancellationToken token) => lines.Reader.ReadAsync(token);
        public ValueTask DisposeAsync() { Disposed = true; lines.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }
}
