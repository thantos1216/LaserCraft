using System.Net;
using System.Net.Sockets;
using System.Text;
using LaserCraft.Devices;
using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class GrblModalTcpTests
{
    [Fact]
    public async Task FragmentedTcpRepliesConsumeAckBeforeTheNextQuery()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8)); var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start();
        try
        {
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            var server = Task.Run(async () =>
            {
                using var peer = await listener.AcceptTcpClientAsync(timeout.Token); var stream = peer.GetStream();
                async Task Expect(string command, string reply)
                {
                    var bytes = new byte[command.Length]; await stream.ReadExactlyAsync(bytes, timeout.Token); Assert.Equal(command, Encoding.ASCII.GetString(bytes));
                    foreach (var b in Encoding.ASCII.GetBytes(reply)) await stream.WriteAsync(new byte[] { b }, timeout.Token);
                }
                await Expect("$I\n", "[VER:1.1h:loopback]\r\nok\r\n");
                await Expect("$G\n", "[MSG:status follows]\r\n<Idle|FS:0,0>\r\n[GC:G0 G54 G17 G21 G90 G94 M5 M9 T0 S0 F0]\r\nok\r\n");
                await Expect("?", "<Idle|MPos:1.25,2.5,0|FS:0,0>\r\n");
            }, timeout.Token);
            var transport = await TransportFactory.OpenAsync(DeviceProfile.Simulator() with { Connection = ConnectionKind.Tcp, Host = "127.0.0.1", Port = port }, timeout.Token);
            await using var monitor = await GrblMonitor.ConnectAsync(transport, timeout.Token);
            Assert.Null(monitor.Simulation); var modal = await monitor.QueryModalStateAsync(timeout.Token); Assert.Equal("G21", modal.Units);
            var status = await monitor.QueryStatusAsync(timeout.Token); Assert.Equal(1.25, status.X); Assert.Equal(2.5, status.Y); await server;
        }
        finally { listener.Stop(); }
    }
}
