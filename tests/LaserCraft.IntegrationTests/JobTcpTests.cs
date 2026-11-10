using System.Net;
using System.Net.Sockets;
using System.Text;
using LaserCraft.Domain;
using LaserCraft.Devices;
namespace LaserCraft.IntegrationTests;
public sealed class JobTcpTests
{
    [Fact]public async Task DocumentJobUsesPublicTcpAdapterWithFragmentedReplies()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var token=timeout.Token;var received=new List<string>();
        var server=Task.Run(async()=>{using var peer=await listener.AcceptTcpClientAsync(token);var stream=peer.GetStream();var pending=new StringBuilder();bool moved=false,finished=false;
            async Task Reply(string text){foreach(var b in Encoding.ASCII.GetBytes(text))await stream.WriteAsync(new byte[]{b},token);}
            var one=new byte[1];while(await stream.ReadAsync(one,token)>0){var b=one[0];if(b==(byte)'?'){received.Add("?");await Reply("<Idle|MPos:-59,-75,0|FS:0,0>\n");if(finished)return;continue;}if(b!=10){pending.Append((char)b);continue;}var line=pending.ToString();pending.Clear();received.Add(line);
                if(line=="$I")await Reply("[VER:1.1h:test]\nok\n");else if(line=="$$")await Reply("$13=0\n$22=1\n$30=1000.000\n$31=0.000\n$32=1\n$130=430.000\n$131=430.000\nok\n");else{if(line.StartsWith("G53",StringComparison.Ordinal))moved=true;if(line=="M9"&&moved)finished=true;await Reply("ok\n");}
            }
        },token);
        var profile=DeviceProfile.Simulator() with {Connection=ConnectionKind.Tcp,Host="127.0.0.1",Port=((IPEndPoint)listener.LocalEndpoint).Port,BedWidth=430,BedHeight=430};var transport=await TransportFactory.OpenAsync(profile,token);await using var client=await GrblMonitor.ConnectAsync(transport,token);
        await client.ExecuteManualAsync(profile,new(GrblManualAction.Home),true,cancellationToken:token);
        var plan=ToolpathPlanner.Build(DesignDocument.Empty() with {Width=430,Height=430,Shapes=[new(Guid.NewGuid(),ShapeKind.Rectangle,10,10,5,5)]});await client.ExecuteJobAsync(profile,plan,true,token:token);await server;Assert.Contains(received,l=>l.StartsWith("G53 G1 X-",StringComparison.Ordinal));Assert.Equal("?",received[^1]);Assert.False(client.RequiresReconnect);
    }
}
