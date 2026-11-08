using LaserCraft.Domain;
using System.IO.Ports;
using System.Net.Sockets;
using System.Text;
namespace LaserCraft.Devices;

public static class TransportFactory
{
    public static string[] SerialPorts() => SerialPort.GetPortNames().Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public static async Task<IGrblTransport> OpenAsync(DeviceProfile profile, CancellationToken cancellationToken = default)
    {
        profile.Validate();
        if (!ControllerCatalog.HasConnectionAdapter(profile.Controller)) throw new NotSupportedException(ControllerCatalog.Support(profile.Controller));
        return profile.Connection switch
        {
            ConnectionKind.Simulator => new SimulatedGrblTransport(),
            ConnectionKind.Serial => await OpenSerialAsync(profile, cancellationToken),
            ConnectionKind.Tcp => await TcpGrblTransport.OpenAsync(profile, cancellationToken),
            _ => throw new NotSupportedException("Unknown transport.")
        };
    }
    private static async Task<IGrblTransport> OpenSerialAsync(DeviceProfile profile, CancellationToken cancellationToken)
    {
        var transport = await Task.Run<IGrblTransport>(() => new SerialGrblTransport(profile), cancellationToken);
        try
        {
            await Task.Delay(profile.SerialStartupDelayMs, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested(); return transport;
        }
        catch { await transport.DisposeAsync(); throw; }
    }
}
internal sealed class SerialGrblTransport : IGrblTransport
{
    private readonly SerialPort port;
    public SerialGrblTransport(DeviceProfile profile)
    {
        port = new(profile.SerialPort, profile.BaudRate, Parity.None, 8, StopBits.One) { Handshake = Handshake.None, DtrEnable = profile.Dtr, RtsEnable = profile.Rts, ReadTimeout = 200, WriteTimeout = 1000, Encoding = Encoding.ASCII };
        try { port.Open(); } catch { port.Dispose(); throw; }
    }
    public async ValueTask WriteAsync(string command, CancellationToken cancellationToken) => await Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); try { port.Write(command); } catch (TimeoutException ex) { throw new IOException("Serial write timed out. Check the connection and baud rate.", ex); } }, cancellationToken);
    public async ValueTask<string> ReadLineAsync(CancellationToken cancellationToken) => await Task.Run(() =>
    {
        var line = new StringBuilder();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested(); int value;
            try { value = port.ReadByte(); } catch (TimeoutException) { continue; }
            if (value < 0) throw new EndOfStreamException("Serial device disconnected."); if (value == '\n') return line.ToString().TrimEnd('\r');
            if (line.Length >= 2048) throw new InvalidDataException("Controller line exceeds 2048 bytes."); line.Append((char)value);
        }
    }, cancellationToken);
    public ValueTask DisposeAsync() { port.Dispose(); return ValueTask.CompletedTask; }
}
internal sealed class TcpGrblTransport : IGrblTransport
{
    private readonly TcpClient client;
    private TcpGrblTransport(TcpClient client) => this.client = client;
    public static async Task<IGrblTransport> OpenAsync(DeviceProfile profile, CancellationToken cancellationToken)
    {
        var client = new TcpClient(); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(5));
        try { await client.ConnectAsync(profile.Host, profile.Port, timeout.Token); return new TcpGrblTransport(client); } catch { client.Dispose(); throw; }
    }
    public async ValueTask WriteAsync(string command, CancellationToken cancellationToken) => await client.GetStream().WriteAsync(Encoding.ASCII.GetBytes(command), cancellationToken);
    public async ValueTask<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        var line = new StringBuilder(); var buffer = new byte[1];
        while (await client.GetStream().ReadAsync(buffer, cancellationToken) > 0)
        {
            if (buffer[0] == '\n') return line.ToString().TrimEnd('\r'); if (line.Length >= 2048) throw new InvalidDataException("Controller line exceeds 2048 bytes."); line.Append((char)buffer[0]);
        }
        throw new EndOfStreamException("TCP device disconnected.");
    }
    public ValueTask DisposeAsync() { client.Dispose(); return ValueTask.CompletedTask; }
}
