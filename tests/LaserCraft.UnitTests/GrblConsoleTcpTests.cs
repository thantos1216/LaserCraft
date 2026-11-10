using System.Net;
using System.Net.Sockets;
using System.Text;
using LaserCraft.Devices;
using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class GrblConsoleTcpTests
{
    [Fact]
    public async Task ConsoleSendsExactSettingAndGcodeBytesAndReadsFragmentedReplies()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8)); var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = Task.Run(async () =>
            {
                using var peer = await listener.AcceptTcpClientAsync(timeout.Token); var stream = peer.GetStream();
                async Task Exchange(string expected, string reply)
                {
                    var bytes = new byte[expected.Length]; await stream.ReadExactlyAsync(bytes, timeout.Token); Assert.Equal(expected, Encoding.ASCII.GetString(bytes));
                    foreach (var b in Encoding.ASCII.GetBytes(reply)) await stream.WriteAsync(new byte[] { b }, timeout.Token);
                }
                await Exchange("$I\n", "[VER:1.1h:console-loopback]\r\nok\r\n");
                await Exchange("$32=1\n", "[MSG:setting accepted]\r\n<Idle|FS:0,0>\r\nok\r\n");
                await Exchange("$$\n", "$30=1000\r\n$32=1\r\nok\r\n");
                await Exchange("G0 X1\n", "error:20\r\n");
                await Exchange("?", "<Idle|MPos:1,2,0|FS:0,0>\r\n");
            }, timeout.Token);
            var transport = await TransportFactory.OpenAsync(DeviceProfile.Simulator() with { Connection = ConnectionKind.Tcp, Host = "127.0.0.1", Port = port }, timeout.Token);
            await using var client = await GrblMonitor.ConnectAsync(transport, timeout.Token); Assert.Null(client.Simulation);
            var result = await client.SubmitConsoleAsync("$32=1", true, cancellationToken: timeout.Token); Assert.Equal(GrblConsoleOutcome.Acknowledged, result.Outcome); Assert.Equal(3, result.Lines.Count);
            Assert.Equal("1", (await client.QuerySettingsAsync(timeout.Token)).Single(s => s.Number == 32).Value);
            Assert.Equal(GrblConsoleOutcome.Rejected, (await client.SubmitConsoleAsync("G0 X1", true, cancellationToken: timeout.Token)).Outcome);
            Assert.Equal(GrblConsoleOutcome.Status, (await client.SubmitConsoleAsync("?", cancellationToken: timeout.Token)).Outcome); await server;
        }
        finally { listener.Stop(); }
    }
    [Fact]
    public async Task SimulatedSettingRangesAndReportUnitsAreAppliedWithoutChangingProfile()
    {
        var profile = DeviceProfile.Simulator(); var transport = new SimulatedGrblTransport(profile); await using var client = await GrblMonitor.ConnectAsync(transport);
        transport.Simulation.Jog(25.4, 50.8, 600);
        Assert.Equal(GrblConsoleOutcome.Rejected, (await client.SubmitConsoleAsync("$32=2", true)).Outcome);
        Assert.Equal(GrblConsoleOutcome.Rejected, (await client.SubmitConsoleAsync("$130=0", true)).Outcome);
        await client.SubmitConsoleAsync("$13=1", true); var status = await client.QueryStatusAsync(); Assert.Equal(1, status.X); Assert.Equal(2, status.Y);
        Assert.Equal("G21", (await client.QueryModalStateAsync()).Units); Assert.Equal(1000, profile.MaxPower);
        await client.SubmitConsoleAsync("$30=255", true); Assert.Equal("255", (await client.QuerySettingsAsync()).Single(s => s.Number == 30).Value); Assert.Equal(1000, profile.MaxPower);
    }
}
