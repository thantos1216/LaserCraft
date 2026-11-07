using LaserCraft.Domain;
using LaserCraft.Devices;
using System.Net;
using System.Net.Sockets;
using System.Text;
namespace LaserCraft.UnitTests;

public sealed class DeviceTests
{
    [Fact]
    public async Task DisconnectCancelsAnOutstandingStatusRead()
    {
        var transport = new BlockingTransport(); var monitor = await GrblMonitor.ConnectAsync(transport); var query = monitor.QueryStatusAsync(); await transport.StatusRequested.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await monitor.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(2)); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => query); Assert.True(transport.Disposed);
    }
    [Fact]
    public async Task UnidentifiedPeerIsRejectedAndTransportClosed()
    {
        var transport = new BlockingTransport { RejectIdentity = true }; await Assert.ThrowsAsync<InvalidDataException>(() => GrblMonitor.ConnectAsync(transport)); Assert.True(transport.Disposed);
    }
    private sealed class BlockingTransport : IGrblTransport
    {
        private readonly Queue<string> lines = [];
        public bool RejectIdentity { get; init; }
        public bool Disposed { get; private set; }
        public TaskCompletionSource StatusRequested { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ValueTask WriteAsync(string command, CancellationToken token)
        {
            token.ThrowIfCancellationRequested(); if (command == "$I\n") { if (!RejectIdentity) lines.Enqueue("[VER:1.1h:test]"); lines.Enqueue("ok"); } else StatusRequested.TrySetResult(); return ValueTask.CompletedTask;
        }
        public async ValueTask<string> ReadLineAsync(CancellationToken token) { if (lines.TryDequeue(out var line)) return line; await Task.Delay(Timeout.Infinite, token); throw new InvalidOperationException(); }
        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
    [Fact]
    public async Task SimulatorOnlyReceivesIdentityAndStatusQueries()
    {
        var transport = new SimulatedGrblTransport(); await using var client = await GrblMonitor.ConnectAsync(transport); var s = await client.QueryStatusAsync(); Assert.Equal("Idle", s.State); Assert.Equal("MPos", s.Coordinates); Assert.Equal(new[] { "$I\n", "?" }, transport.Commands);
    }
    [Fact] public void InvalidCoordinateMessagesAreRejected() => Assert.Throws<InvalidDataException>(() => GrblStatus.Parse("<Idle|MPos:NaN,0,0>"));
    [Fact] public void StatusWithoutPositionDoesNotInventCoordinates() { var s = GrblStatus.Parse("<Alarm|FS:0,0>"); Assert.Null(s.X); Assert.Equal("Alarm", s.State); }
    [Theory]
    [InlineData(ConnectionKind.Serial, "", 23)]
    [InlineData(ConnectionKind.Tcp, "", 23)]
    [InlineData(ConnectionKind.Tcp, "localhost", 70000)]
    public void InvalidEndpointsAreRejected(ConnectionKind connection, string host, int port) => Assert.Throws<InvalidDataException>(() => (DeviceProfile.Simulator() with { Connection = connection, Host = host, Port = port }).Validate());
    [Fact] public async Task UnsupportedControllerIsNotSentGrblCommands() => await Assert.ThrowsAsync<NotSupportedException>(() => TransportFactory.OpenAsync(DeviceProfile.Simulator() with { Controller = ControllerFamily.Ruida }));
    [Fact]
    public void ProfilesRoundTripIncludingAirAndOrigin()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json"); var p = DeviceProfile.Simulator() with { AirAssistEnabled = true, Origin = MachineOrigin.TopRight, MaxPower = 255 };
        try { ProfileStore.Save(path, [p]); Assert.Equal(p, Assert.Single(ProfileStore.Load(path))); } finally { File.Delete(path); }
    }
    [Fact]
    public async Task TcpAdapterExchangesQueriesWithLoopbackController()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8)); var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            var server = Task.Run(async () =>
            {
                using var peer = await listener.AcceptTcpClientAsync(timeout.Token); var stream = peer.GetStream(); var bytes = new byte[3]; await stream.ReadExactlyAsync(bytes, timeout.Token); Assert.Equal("$I\n", Encoding.ASCII.GetString(bytes)); await stream.WriteAsync(Encoding.ASCII.GetBytes("[VER:1.1h:loopback]\nok\n"), timeout.Token); var query = new byte[1]; await stream.ReadExactlyAsync(query, timeout.Token); Assert.Equal((byte)'?', query[0]); await stream.WriteAsync(Encoding.ASCII.GetBytes("<Idle|WPos:1.2,3.4,5.6>\n"), timeout.Token);
            }, timeout.Token);
            var profile = DeviceProfile.Simulator() with { Connection = ConnectionKind.Tcp, Host = "127.0.0.1", Port = port }; var transport = await TransportFactory.OpenAsync(profile, timeout.Token); await using var monitor = await GrblMonitor.ConnectAsync(transport, timeout.Token); var status = await monitor.QueryStatusAsync(timeout.Token); Assert.Equal(1.2, status.X); Assert.Equal("WPos", status.Coordinates); await server;
        }
        finally { listener.Stop(); }
    }
}
