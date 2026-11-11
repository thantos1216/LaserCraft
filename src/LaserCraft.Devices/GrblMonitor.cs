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
public sealed partial class GrblMonitor : IAsyncDisposable
{
    private readonly IGrblTransport transport;
    private readonly SemaphoreSlim gate = new(1);
    private readonly CancellationTokenSource lifetime = new();
    private int disposalStarted;
    private int consoleUncertain;
    private void RequireSynchronizedConsole()
    {
        if (Volatile.Read(ref consoleUncertain) != 0) throw new IOException("The console session requires reconnection after an interrupted exchange or alarm. Disconnect and reconnect before further commands; reconnecting does not stop controller motion.");
    }
    private int homingConfirmed;
    private LaserCraft.Domain.PointMm? homedMachinePosition;
    private LaserCraft.Domain.PointMm? userOrigin;
    public LaserCraft.Domain.PointMm? UserOrigin => userOrigin;
    public bool HomingConfirmed => Volatile.Read(ref homingConfirmed)!=0;
    public bool RequiresReconnect => Volatile.Read(ref consoleUncertain) != 0;
    public string Identity { get; private set; } = "";
    public ManualControlSimulation? Simulation { get; }
    private GrblMonitor(IGrblTransport transport, ConnectionJournal? journal)
    {
        Simulation = (transport as SimulatedGrblTransport)?.Simulation;
        this.transport = new SerializedGrblTransport(journal is null ? transport : new JournaledGrblTransport(transport, journal));
    }
    public static async Task<GrblMonitor> ConnectAsync(IGrblTransport transport, CancellationToken cancellationToken = default, ConnectionJournal? journal = null)
    {
        var monitor = new GrblMonitor(transport, journal); transport = monitor.transport;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken); timeout.CancelAfter(TimeSpan.FromSeconds(6));
            await transport.WriteAsync("$I\n", timeout.Token);
            for (int i = 0; i < 64; i++)
            {
                var line = await transport.ReadLineAsync(timeout.Token);
                if (line.StartsWith("[VER:", StringComparison.Ordinal) || line.StartsWith("Grbl", StringComparison.OrdinalIgnoreCase)) monitor.Identity = line;
                // Reset during motion can emit ALARM:3 before the allowed identity reply.
                // Keep the alarm lock: identity + ok are still required; never send $X here.
                if (line == "ALARM:3") continue;
                if (line.StartsWith("error:") || line.StartsWith("ALARM:")) throw new IOException("Controller rejected identity query: " + line);
                if (line == "ok") { if (monitor.Identity.Length == 0) throw new InvalidDataException("Controller did not identify a GRBL-compatible firmware."); return monitor; }
            }
            throw new InvalidDataException("Too many unexpected controller messages.");
        }
        catch { await monitor.DisposeAsync(); throw; }
    }
    public async Task<GrblStatus> QueryStatusAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposalStarted) != 0, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(4));
        await gate.WaitAsync(timeout.Token);
        try
        {
            if (Volatile.Read(ref disposalStarted) != 0) throw new OperationCanceledException("Connection is closing.", lifetime.Token);
            RequireSynchronizedConsole(); timeout.Token.ThrowIfCancellationRequested(); await transport.WriteAsync("?", timeout.Token);
            for (int i = 0; i < 64; i++) { var line = await transport.ReadLineAsync(timeout.Token); if (line.StartsWith('<')) return GrblStatus.Parse(line); if (line.StartsWith("error:") || line.StartsWith("ALARM:")) throw new IOException(line); }
            throw new InvalidDataException("No status frame within the response limit.");
        }
        finally { gate.Release(); }
    }
    public async Task<GrblModalState> QueryModalStateAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposalStarted) != 0, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(4));
        await gate.WaitAsync(timeout.Token);
        try
        {
            if (Volatile.Read(ref disposalStarted) != 0) throw new OperationCanceledException("Connection is closing.", lifetime.Token);
            RequireSynchronizedConsole(); timeout.Token.ThrowIfCancellationRequested(); await transport.WriteAsync("$G\n", timeout.Token); GrblModalState? modal = null;
            for (int i = 0; i < 64; i++)
            {
                var line = await transport.ReadLineAsync(timeout.Token);
                if (line == "ok") return modal ?? throw new InvalidDataException("Parser-state query acknowledged without a report.");
                if (line.StartsWith("error:") || line.StartsWith("ALARM:")) throw new IOException("Controller rejected parser-state query: " + line);
                if (!line.StartsWith("[GC:", StringComparison.Ordinal)) continue;
                if (modal is not null) throw new InvalidDataException("Duplicate parser-state report.");
                modal = GrblModalState.Parse(line);
            }
            throw new InvalidDataException("Parser-state response exceeded 64 lines without completion.");
        }
        finally { gate.Release(); }
    }
    public async Task<IReadOnlyList<GrblSetting>> QuerySettingsAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposalStarted) != 0, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(6));
        await gate.WaitAsync(timeout.Token);
        try
        {
            if (Volatile.Read(ref disposalStarted) != 0) throw new OperationCanceledException("Connection is closing.", lifetime.Token);
            RequireSynchronizedConsole(); timeout.Token.ThrowIfCancellationRequested(); await transport.WriteAsync("$$\n", timeout.Token);
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
    public async Task<GrblConsoleResult> SubmitConsoleAsync(string input, bool allowManual = false, int timeoutSeconds = 10, CancellationToken cancellationToken = default)
    {
        var command = GrblConsoleCommand.Prepare(input, allowManual);
        if (timeoutSeconds < 1 || timeoutSeconds > 120) throw new InvalidDataException("Console timeout must be 1–120 seconds.");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposalStarted) != 0, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        await gate.WaitAsync(timeout.Token);
        try
        {
            if (Volatile.Read(ref disposalStarted) != 0) throw new OperationCanceledException("Connection is closing.", lifetime.Token);
            RequireSynchronizedConsole(); timeout.Token.ThrowIfCancellationRequested();
            if(!command.IsQuery)Volatile.Write(ref homingConfirmed,0);
            return await ExchangeConsoleCore(command, timeout.Token);
        }
        finally { gate.Release(); }
    }
    private async Task<GrblConsoleResult> ExchangeConsoleCore(GrblConsoleCommand command, CancellationToken token, Action<GrblStatus>? observeStatus=null)
    {
        // Once a write is attempted, an interrupted read cannot prove whether it executed.
        Volatile.Write(ref consoleUncertain, 1); await transport.WriteAsync(command.Wire, token);
        var lines = new List<string>();
        System.Diagnostics.Stopwatch? idleWithoutAck=null;
        for (int i = 0; i < 256; i++)
        {
            var line = observeStatus is null ? await transport.ReadLineAsync(token) : await ReadJobResponseAsync(token); if (line.Length > 2048) throw new InvalidDataException("Console response line exceeds 2048 characters."); lines.Add(line);
            if(observeStatus is not null && line.StartsWith('<'))
            {
                var status=GrblStatus.Parse(line);observeStatus(status);
                if(command.Text!="?" && status.State=="Idle")
                {
                    idleWithoutAck ??= System.Diagnostics.Stopwatch.StartNew();
                    if(idleWithoutAck.Elapsed>=TimeSpan.FromSeconds(10))throw new IOException("Controller reports Idle but the pending command acknowledgement is missing. Completion is unknown; reconnect. No retry.");
                }
                else idleWithoutAck=null;
            }
            if (line.StartsWith("Grbl", StringComparison.OrdinalIgnoreCase)) throw new IOException("Controller restarted during the console exchange.");
            GrblConsoleOutcome? outcome = line == "ok" ? GrblConsoleOutcome.Acknowledged : line.StartsWith("error:", StringComparison.Ordinal) ? GrblConsoleOutcome.Rejected : line.StartsWith("ALARM:", StringComparison.Ordinal) ? GrblConsoleOutcome.Alarm : null;
            if (command.Text == "?" && line.StartsWith('<')) { GrblStatus.Parse(line); outcome = GrblConsoleOutcome.Status; }
            if (outcome is null) continue;
            if (outcome == GrblConsoleOutcome.Acknowledged && command.Text == "?") continue;
            Volatile.Write(ref consoleUncertain, outcome == GrblConsoleOutcome.Alarm ? 1 : 0); return new(outcome.Value, lines.AsReadOnly());
        }
        throw new InvalidDataException("Console response exceeded 256 lines without a terminal response.");
    }
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref disposalStarted, 1) != 0) return; await lifetime.CancelAsync(); await gate.WaitAsync();
        try { await transport.DisposeAsync(); } finally { gate.Release(); }
    }
}
public sealed class SimulatedGrblTransport : IGrblTransport
{
    public ManualControlSimulation Simulation { get; }
    private readonly SortedDictionary<int, string> settings;
    public SimulatedGrblTransport(LaserCraft.Domain.DeviceProfile? profile = null)
    {
        Simulation = new(profile ?? LaserCraft.Domain.DeviceProfile.Simulator());
        settings = new() { [10] = "1", [13] = "0", [30] = Simulation.Profile.MaxPower.ToString(CultureInfo.InvariantCulture), [31] = "0", [32] = "1", [130] = Simulation.Profile.BedWidth.ToString("0.000", CultureInfo.InvariantCulture), [131] = Simulation.Profile.BedHeight.ToString("0.000", CultureInfo.InvariantCulture) };
    }
    private readonly Channel<string> replies = Channel.CreateUnbounded<string>();
    public List<string> Commands { get; } = [];
    public ValueTask WriteAsync(string command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested(); Commands.Add(command);
        switch (command)
        {
            case "$I\n": replies.Writer.TryWrite("[VER:1.1h:LaserCraft simulator]"); replies.Writer.TryWrite("ok"); break;
            case "?":
                var reportingScale = double.Parse(settings[13], CultureInfo.InvariantCulture) == 1 ? 25.4 : 1;
                replies.Writer.TryWrite(FormattableString.Invariant($"<{(Simulation.IsPulsing ? "Run" : "Idle")}|MPos:{Simulation.Position.X / reportingScale:0.000},{Simulation.Position.Y / reportingScale:0.000},0.000|FS:0,{Simulation.SValue}>")); break;
            case "$G\n": replies.Writer.TryWrite(FormattableString.Invariant($"[GC:G0 G54 G17 G21 G90 G94 M{(Simulation.IsPulsing ? 3 : 5)} M9 T0 S{Simulation.SValue} F0]")); replies.Writer.TryWrite("ok"); break;
            case "$$\n": foreach (var setting in settings) replies.Writer.TryWrite($"${setting.Key.ToString(CultureInfo.InvariantCulture)}={setting.Value}"); replies.Writer.TryWrite("ok"); break;
            case "$\n": replies.Writer.TryWrite("[HLP:LaserCraft simulator: $I $$ $G $# $N; numeric settings are simulated; custom G-code returns error:20]"); replies.Writer.TryWrite("ok"); break;
            case "$#\n": replies.Writer.TryWrite("[G54:0.000,0.000,0.000]"); replies.Writer.TryWrite("ok"); break;
            case "$N\n": replies.Writer.TryWrite("$N0="); replies.Writer.TryWrite("$N1="); replies.Writer.TryWrite("ok"); break;
            default:
                var match = System.Text.RegularExpressions.Regex.Match(command, "^\\$(\\d{1,3})=([+-]?(?:[0-9]+(?:\\.[0-9]*)?|\\.[0-9]+))\\n$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
                if (match.Success && int.TryParse(match.Groups[1].Value, out var number) && settings.ContainsKey(number) && double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) && ValidSetting(number, value))
                { settings[number] = match.Groups[2].Value; replies.Writer.TryWrite("ok"); }
                else replies.Writer.TryWrite("error:20");
                break;
        }
        return ValueTask.CompletedTask;
    }
    private static bool ValidSetting(int number, double value) => number switch { 13 or 32 => value is 0 or 1, 10 => value >= 0 && value <= 255 && value == Math.Truncate(value), 30 => value > 0 && value <= 1000000, 31 => value >= 0 && value <= 1000000, 130 or 131 => value > 0 && value <= 10000, _ => false };
    public ValueTask<string> ReadLineAsync(CancellationToken cancellationToken) => replies.Reader.ReadAsync(cancellationToken);
    public ValueTask DisposeAsync() { Simulation.Stop(); replies.Writer.TryComplete(); return ValueTask.CompletedTask; }
}
