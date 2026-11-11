using System.Net;
using System.Net.Sockets;
using System.Text;
using LaserCraft.Devices;
using LaserCraft.Domain;
namespace LaserCraft.IntegrationTests;

public sealed class JobTcpRecoveryTests
{
    [Fact]
    public async Task AbortClosesUncertainSessionAndFreshAlarmConnectionCanHomeAndJog()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));var token=timeout.Token;
        var firstMotion=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received=new List<(int Session,string Command)>();
        var server=Task.Run(async()=>
        {
            for(int session=0;session<2;session++)
            {
                using var peer=await listener.AcceptTcpClientAsync(token);var stream=peer.GetStream();
                var pending=new StringBuilder();var one=new byte[1];var alarm=session==1;
                async Task Reply(string text)=>await stream.WriteAsync(Encoding.ASCII.GetBytes(text),token);
                try { while(await stream.ReadAsync(one,token)>0)
                {
                    var b=one[0];
                    if(b==0x18){received.Add((session,"reset"));await Reply("Grbl 1.1h reset\nALARM:3\n");continue;}
                    if(b=='?'){received.Add((session,"?"));await Reply($"<{(alarm?"Alarm":"Idle")}|MPos:1,1,0|FS:0,0>\n");continue;}
                    if(b!=10){pending.Append((char)b);continue;}
                    var command=pending.ToString();pending.Clear();received.Add((session,command));
                    if(command=="$I")await Reply((session==1?"Grbl 1.1h reset\nALARM:3\n":"")+"[VER:1.1h:test]\nok\n");
                    else if(command=="$$")await Reply("$13=0\n$22=1\n$30=1000\n$31=0\n$32=1\n$130=430\n$131=430\nok\n");
                    else if(session==0&&command.StartsWith("G53")){firstMotion.TrySetResult(); /* Lost ack: abort must cancel its reader. */}
                    else {if(command=="$H")alarm=false;await Reply("ok\n");}
                } }
                catch(IOException)when(session==0){ /* Closing the cancelled stream can reset TCP. */ }
                catch(SocketException)when(session==0){ /* Same expected close at the socket boundary. */ }
            }
        },token);
        var profile=DeviceProfile.Simulator() with {Connection=ConnectionKind.Tcp,Host="127.0.0.1",Port=((IPEndPoint)listener.LocalEndpoint).Port,BedWidth=430,BedHeight=430};
        await using var first=await GrblMonitor.ConnectAsync(await TransportFactory.OpenAsync(profile,token),token);
        await first.ExecuteManualAsync(profile,new(GrblManualAction.Home),true,cancellationToken:token);
        var plan=ToolpathPlanner.Build(DesignDocument.Empty() with {Width=430,Height=430,Shapes=[new(Guid.NewGuid(),ShapeKind.Rectangle,10,10,5,5)]});
        var job=first.ExecuteJobAsync(profile,plan,true,token:token);await firstMotion.Task.WaitAsync(token);
        await first.StopJobAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>job);Assert.False(first.JobActive);await first.DisposeAsync();
        await using var fresh=await GrblMonitor.ConnectAsync(await TransportFactory.OpenAsync(profile,token),token);
        Assert.Equal("Alarm",(await fresh.QueryStatusAsync(token)).State);Assert.DoesNotContain(received,x=>x.Session==1&&x.Command=="$H");
        await fresh.ExecuteManualAsync(profile,new(GrblManualAction.Home),true,cancellationToken:token);
        await fresh.ExecuteManualAsync(profile,new(GrblManualAction.JogX),true,cancellationToken:token);
        await fresh.DisposeAsync();await server;
        Assert.Single(received,x=>x.Session==0&&x.Command.StartsWith("G53"));Assert.Single(received,x=>x.Command=="reset");
        Assert.Contains(received,x=>x.Session==1&&x.Command.StartsWith("$J="));Assert.DoesNotContain(received,x=>x.Command=="$X");
    }
}
