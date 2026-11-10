using System.Net;
using System.Net.Sockets;
using System.Text;
using LaserCraft.Domain;
using LaserCraft.Devices;
namespace LaserCraft.IntegrationTests;
public sealed class ManualTcpTests
{
    [Fact] public async Task PhysicalControlProtocolUsesExactWireBytesAgainstLocalPeer()
    {
        using var listener = new TcpListener(IPAddress.Loopback,0);listener.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var token=timeout.Token;
        var expected = new (string Wire,string Reply)[] {
            ("$I\n","[VER:1.1h:loopback]\nok\n"), ("?","<Idle|MPos:-10,-20,0|FS:0,0>\n"), ("M5 S0\n","ok\n"), ("$J=G21 G91 X1 F600\n","ok\n"),
            ("?","<Idle|MPos:-9,-20,0|FS:0,0>\n"), ("M5 S0\n","ok\n"), ("$$\n","$30=1000\n$31=0\n$32=1\nok\n"),
            ("G1 G94 F60 M3 S10\n","ok\n"), ("G4 P0.1\n","ok\n"), ("M5 S0\n","ok\n"), ("\u0085",""), ("M5 S0\n","ok\n") };
        var server=Task.Run(async()=>{
            using var socket=await listener.AcceptTcpClientAsync(token);var stream=socket.GetStream();
            foreach(var step in expected){var want=Encoding.Latin1.GetBytes(step.Wire);var received=new byte[want.Length];await stream.ReadExactlyAsync(received,token);Assert.Equal(want,received);foreach(var b in Encoding.ASCII.GetBytes(step.Reply))await stream.WriteAsync(new byte[]{b},token);}
        },token);
        var profile=DeviceProfile.Simulator() with {Connection=ConnectionKind.Tcp,Host="127.0.0.1",Port=((IPEndPoint)listener.LocalEndpoint).Port};
        var transport=await TransportFactory.OpenAsync(profile,token);await using var client=await GrblMonitor.ConnectAsync(transport,token);
        await client.ExecuteManualAsync(profile,new(GrblManualAction.JogX),true,cancellationToken:token);
        await client.ExecuteManualAsync(profile,new(GrblManualAction.Pulse),true,true,token);
        await client.ExecuteManualAsync(profile,new(GrblManualAction.CancelJog),true,cancellationToken:token);
        await client.ExecuteManualAsync(profile,new(GrblManualAction.LaserOff),true,cancellationToken:token);
        await server;Assert.False(client.RequiresReconnect);
    }
}
