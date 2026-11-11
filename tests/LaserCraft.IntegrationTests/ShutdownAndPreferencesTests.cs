using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using LaserCraft.Desktop;
using LaserCraft.Devices;
using LaserCraft.Domain;
namespace LaserCraft.IntegrationTests;
public sealed class ShutdownAndPreferencesTests
{
    [Fact]public async Task DisposeCancelsPendingStatusAndClosesTheActualTcpEndpoint()
    {
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));var token=timeout.Token;
        var waiting=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server=Task.Run(async()=>{using var socket=await listener.AcceptTcpClientAsync(token);var stream=socket.GetStream();var one=new byte[1];var text=new StringBuilder();while(await stream.ReadAsync(one,token)>0){if(one[0]=='?'){waiting.TrySetResult();continue;}if(one[0]!=10){text.Append((char)one[0]);continue;}Assert.Equal("$I",text.ToString());text.Clear();await stream.WriteAsync(Encoding.ASCII.GetBytes("[VER:1.1h:test]\nok\n"),token);}},token);
        var profile=DeviceProfile.Simulator() with {Connection=ConnectionKind.Tcp,Host="127.0.0.1",Port=((IPEndPoint)listener.LocalEndpoint).Port};
        await using var client=await GrblMonitor.ConnectAsync(await TransportFactory.OpenAsync(profile,token),token);var query=client.QueryStatusAsync(token);await waiting.Task.WaitAsync(token);await client.DisposeAsync();await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>query);await server.WaitAsync(token);
    }
    [Fact]public void PreferencesRoundTripPerDeviceAndMigrateThemeOnlySettings()
    {
        var a=Guid.NewGuid();var b=Guid.NewGuid();var p=new WorkspacePreferences(true,b,true,new(){[a]=new(1,600,1,100),[b]=new(31.75m,1200,2,250,Start:2,Anchor:4,Port:"COM5")},true);
        var copy=JsonSerializer.Deserialize<WorkspacePreferences>(JsonSerializer.Serialize(p))!;Assert.Equal(b,copy.Device);Assert.True(copy.Inches);Assert.True(copy.FrameBeforeRun);Assert.Equal(31.75m,copy.Controls![b].Distance);Assert.Equal(600,copy.Controls[a].Feed);Assert.Equal("COM5",copy.Controls[b].Port);
        var legacy=JsonSerializer.Deserialize<WorkspacePreferences>("{\"Light\":true}")!;Assert.True(legacy.Light);Assert.False(legacy.Inches);Assert.Null(legacy.Controls);
    }
}
