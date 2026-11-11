using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using LaserCraft.Domain;
using LaserCraft.Devices;
namespace LaserCraft.IntegrationTests;
public sealed class RepeatedCutTcpTests
{
    [Theory][InlineData(PassOrderMode.WholeLayer)][InlineData(PassOrderMode.PerObject)]
    public async Task TwoFivePassJobsReturnToTheirAnchorAndRepeatIdenticalWirePaths(PassOrderMode order)
    {
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var token=timeout.Token;
        var commands=new List<string>();double x=101,y=101;
        var server=Task.Run(async()=>
        {
            using var socket=await listener.AcceptTcpClientAsync(token);var stream=socket.GetStream();var one=new byte[1];var pending=new StringBuilder();
            async Task Reply(string text)=>await stream.WriteAsync(Encoding.ASCII.GetBytes(text),token);
            while(await stream.ReadAsync(one,token)>0)
            {
                if(one[0]=='?'){await Reply(FormattableString.Invariant($"<Idle|MPos:{x},{y},0|FS:0,0>\n"));continue;}
                if(one[0]!=10){pending.Append((char)one[0]);continue;}var command=pending.ToString();pending.Clear();commands.Add(command);
                if(command=="$I")await Reply("[VER:1.1h:test]\nok\n");
                else if(command=="$$")await Reply("$13=0\n$22=1\n$30=1000\n$31=0\n$32=1\n$130=430\n$131=430\nok\n");
                else
                {
                    if(command.StartsWith("G53"))foreach(var word in command.Split(' ')){if(word.StartsWith('X'))x=double.Parse(word[1..],CultureInfo.InvariantCulture);if(word.StartsWith('Y'))y=double.Parse(word[1..],CultureInfo.InvariantCulture);}
                    await Reply("ok\n");
                }
            }
        },token);
        var profile=DeviceProfile.Simulator() with {Connection=ConnectionKind.Tcp,Host="127.0.0.1",Port=((IPEndPoint)listener.LocalEndpoint).Port,BedWidth=430,BedHeight=430};
        await using var client=await GrblMonitor.ConnectAsync(await TransportFactory.OpenAsync(profile,token),token);await client.ExecuteManualAsync(profile,new(GrblManualAction.Home),true,cancellationToken:token);
        var d=DesignDocument.Empty() with {Width=430,Height=430,Layers=[new("Cut","#000000",Speed:60,Power:80,Passes:5,PassOrder:order)],Shapes=[new(Guid.NewGuid(),ShapeKind.Rectangle,10,10,4,4),new(Guid.NewGuid(),ShapeKind.Rectangle,30,10,4,4)]};var plan=ToolpathPlanner.Build(d);
        var before=commands.Count;await client.ExecuteJobAsync(profile,plan,true,token:token);var first=commands.Skip(before).ToArray();Assert.Equal(101,x);Assert.Equal(101,y);before=commands.Count;
        await client.ExecuteJobAsync(profile,plan,true,token:token);var second=commands.Skip(before).ToArray();Assert.Equal(first,second);Assert.Equal(40,second.Count(c=>c.StartsWith("G53 G1")&&c.EndsWith("F60 S800")));Assert.Equal("G53 G0 X101 Y101 S0",second[^1]);
        await client.DisposeAsync();await server;Assert.False(client.JobActive);
    }
}
