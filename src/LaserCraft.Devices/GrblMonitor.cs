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
    public double? Feed { get; init; }
    public double? Spindle { get; init; }
    public int? PlannerAvailable { get; init; }
    public int? ReceiveAvailable { get; init; }
    public string? Pins { get; init; }
    public string? Accessories { get; init; }
    public static GrblStatus Parse(string line)
    {
        if (!line.StartsWith('<') || !line.EndsWith('>') || line.Length > 2048) throw new InvalidDataException("Invalid GRBL status frame.");
        var fields = line[1..^1].Split('|'); if (string.IsNullOrWhiteSpace(fields[0])) throw new InvalidDataException("Missing GRBL state.");
        string? Field(string name)
        {
            var matches = fields.Skip(1).Where(s => s.StartsWith(name + ":", StringComparison.Ordinal)).ToArray();
            if (matches.Length > 1) throw new InvalidDataException("Duplicate GRBL status field: " + name);
            return matches.Length == 0 ? null : matches[0][(name.Length + 1)..];
        }
        double[]? Numbers(string name, int count)
        {
            var value = Field(name); if (value is null) return null; var parts = value.Split(',');
            if (parts.Length != count) throw new InvalidDataException("Incomplete GRBL " + name + " field.");
            return parts.Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) && n >= 0 ? n : throw new InvalidDataException("Invalid GRBL " + name + " field.")).ToArray();
        }
        var machine = Field("MPos"); var work = Field("WPos"); var pos = machine ?? work;
        GrblStatus result = new(fields[0], null, null, null, "not reported", line);
        if (pos is not null)
        {
            var values = pos.Split(','); if (values.Length < 3) throw new InvalidDataException("Incomplete GRBL position.");
            var numbers = values.Select(s => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : throw new InvalidDataException("Invalid GRBL position.")).ToArray();
            result = result with { X = numbers[0], Y = numbers[1], Z = numbers[2], Coordinates = machine is null ? "WPos" : "MPos" };
        }
        var fs = Numbers("FS", 2); var feed = Numbers("F", 1); var buffers = Numbers("Bf", 2);
        if (buffers is not null && buffers.Any(n => n > int.MaxValue || n != Math.Truncate(n))) throw new InvalidDataException("Invalid GRBL buffer counts.");
        return result with { Feed = fs?[0] ?? feed?[0], Spindle = fs?[1], PlannerAvailable = buffers is null ? null : (int)buffers[0], ReceiveAvailable = buffers is null ? null : (int)buffers[1], Pins = Field("Pn"), Accessories = Field("A") };
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
    public async Task<IReadOnlyList<GrblSetting>> QuerySettingsAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(6));
        await gate.WaitAsync(timeout.Token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this); await transport.WriteAsync("$$\n", timeout.Token);
            var settings = new List<GrblSetting>(); var numbers = new HashSet<int>();
            for (int i = 0; i < 256; i++)
            {
                var line = await transport.ReadLineAsync(timeout.Token);
                if (line == "ok") return settings.Count > 0 ? settings.AsReadOnly() : throw new InvalidDataException("Controller acknowledged the query without reporting settings.");
                if (line.StartsWith("error:") || line.StartsWith("ALARM:")) throw new IOException("Controller rejected settings query: " + line);
                if (!line.StartsWith('$')) continue;
                var setting = GrblSetting.Parse(line);
                if (!numbers.Add(setting.Number)) throw new InvalidDataException("Controller reported duplicate setting $" + setting.Number);
                settings.Add(setting);
            }
            throw new InvalidDataException("Settings response exceeded 256 lines without completion.");
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
        switch (command) { case "$I\n": replies.Writer.TryWrite("[VER:1.1h:LaserCraft simulator]"); replies.Writer.TryWrite("ok"); break; case "?": replies.Writer.TryWrite("<Idle|MPos:0.000,0.000,0.000|FS:0,0>"); break; case "$$\n": foreach (var line in new[] { "$10=1", "$13=0", "$30=1000", "$31=0", "$32=1", "$130=400.000", "$131=400.000", "ok" }) replies.Writer.TryWrite(line); break; default: throw new InvalidOperationException("The monitor only permits identity, status and settings queries."); }
        return ValueTask.CompletedTask;
    }
    public ValueTask<string> ReadLineAsync(CancellationToken cancellationToken) => replies.Reader.ReadAsync(cancellationToken);
    public ValueTask DisposeAsync() { replies.Writer.TryComplete(); return ValueTask.CompletedTask; }
}
