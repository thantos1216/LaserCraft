using System.Globalization;
using System.Threading.Channels;
namespace LaserCraft.Devices;

public interface IGrblTransport : IAsyncDisposable
{
    ValueTask WriteAsync(string command, CancellationToken cancellationToken);
    ValueTask<string> ReadLineAsync(CancellationToken cancellationToken);
}
public sealed record GrblStatus(string State, double? X, double? Y, double? Z, string Coordinates, string Raw)
{
    public static GrblStatus Parse(string line)
    {
        if (!line.StartsWith('<') || !line.EndsWith('>') || line.Length > 2048) throw new InvalidDataException("Invalid GRBL status frame.");
        var fields = line[1..^1].Split('|'); if (string.IsNullOrWhiteSpace(fields[0])) throw new InvalidDataException("Missing GRBL state.");
        var pos = fields.Skip(1).FirstOrDefault(s => s.StartsWith("MPos:") || s.StartsWith("WPos:"));
        if (pos is null) return new(fields[0], null, null, null, "not reported", line);
        var values = pos[5..].Split(','); if (values.Length < 3) throw new InvalidDataException("Incomplete GRBL position.");
        var numbers = values.Take(3).Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : throw new InvalidDataException("Invalid GRBL position.")).ToArray();
        return new(fields[0], numbers[0], numbers[1], numbers[2], pos[..4], line);
    }
}
public sealed class GrblMonitor : IAsyncDisposable
{
    private readonly IGrblTransport transport;
    private readonly SemaphoreSlim gate = new(1);
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    public string Identity { get; private set; } = "";
    private GrblMonitor(IGrblTransport transport) => this.transport = transport;
    public static async Task<GrblMonitor> ConnectAsync(IGrblTransport transport, CancellationToken cancellationToken = default)
    {
        var monitor = new GrblMonitor(transport);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(6));
            await transport.WriteAsync("$I\n", timeout.Token);
            for (int i = 0; i < 64; i++)
            {
                var line = await transport.ReadLineAsync(timeout.Token);
                if (line.StartsWith("[VER:", StringComparison.Ordinal) || line.StartsWith("Grbl", StringComparison.OrdinalIgnoreCase)) monitor.Identity = line;
                if (line.StartsWith("error:") || line.StartsWith("ALARM:")) throw new IOException("Controller rejected identity query: " + line);
                if (line == "ok") { if (monitor.Identity.Length == 0) throw new InvalidDataException("Controller did not identify a GRBL-compatible firmware."); return monitor; }
            }
            throw new InvalidDataException("Too many unexpected controller messages.");
        }
        catch { await monitor.DisposeAsync(); throw; }
    }
    public async Task<GrblStatus> QueryStatusAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(4));
        await gate.WaitAsync(timeout.Token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this); await transport.WriteAsync("?", timeout.Token);
            for (int i = 0; i < 64; i++) { var line = await transport.ReadLineAsync(timeout.Token); if (line.StartsWith('<')) return GrblStatus.Parse(line); if (line.StartsWith("error:")) throw new IOException(line); }
            throw new InvalidDataException("No status frame within the response limit.");
        }
        finally { gate.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return; disposed = true; await lifetime.CancelAsync(); await gate.WaitAsync();
        try { await transport.DisposeAsync(); } finally { gate.Release(); }
    }
}
public sealed class SimulatedGrblTransport : IGrblTransport
{
    private readonly Channel<string> replies = Channel.CreateUnbounded<string>();
    public List<string> Commands { get; } = [];
    public ValueTask WriteAsync(string command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); Commands.Add(command);
        switch (command) { case "$I\n": replies.Writer.TryWrite("[VER:1.1h:LaserCraft simulator]"); replies.Writer.TryWrite("ok"); break; case "?": replies.Writer.TryWrite("<Idle|MPos:0.000,0.000,0.000|FS:0,0>"); break; default: throw new InvalidOperationException("The monitor only permits identity and status queries."); }
        return ValueTask.CompletedTask;
    }
    public ValueTask<string> ReadLineAsync(CancellationToken cancellationToken) => replies.Reader.ReadAsync(cancellationToken);
    public ValueTask DisposeAsync() { replies.Writer.TryComplete(); return ValueTask.CompletedTask; }
}
