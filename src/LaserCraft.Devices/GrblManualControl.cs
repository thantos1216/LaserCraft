using System.Globalization;
using LaserCraft.Domain;

namespace LaserCraft.Devices;

public enum GrblManualAction { JogX, JogY, Pulse, LaserOff, CancelJog }
public sealed record GrblManualRequest(GrblManualAction Action, double Distance = 1, double Feed = 600, double PowerPercent = 1, int Milliseconds = 100)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Action)) throw new InvalidDataException("Unknown manual action.");
        if (Action is GrblManualAction.JogX or GrblManualAction.JogY && (!double.IsFinite(Distance) || Math.Abs(Distance) < .001 || Math.Abs(Distance) > 10 || !double.IsFinite(Feed) || Feed < 60 || Feed > 3000)) throw new InvalidDataException("Physical jogs require 0.001–10 mm and 60–3000 mm/min.");
        if (Action == GrblManualAction.Pulse && (!double.IsFinite(PowerPercent) || PowerPercent <= 0 || PowerPercent > 5 || Milliseconds < 1 || Milliseconds > 500)) throw new InvalidDataException("Physical pulses require >0–5% and 1–500 ms.");
    }
}
public sealed record GrblManualResult(IReadOnlyList<string> Lines, string Summary);

public sealed partial class GrblMonitor
{
    public async Task<GrblManualResult> ExecuteManualAsync(DeviceProfile profile, GrblManualRequest request, bool allowMotion, bool allowFire = false, CancellationToken cancellationToken = default)
    {
        profile.Validate(); request.Validate();
        if (!allowMotion || request.Action == GrblManualAction.Pulse && !allowFire) throw new InvalidDataException("Enable physical control, and the separate firing permission for a pulse.");
        if (profile.Controller != ControllerFamily.Grbl || profile.Connection == ConnectionKind.Simulator || !Identity.StartsWith("[VER:1.1", StringComparison.Ordinal) && !Identity.StartsWith("Grbl 1.1", StringComparison.Ordinal)) throw new InvalidDataException("Guided physical controls require an identified GRBL 1.1 serial/TCP connection. Other firmware remains console-only.");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposalStarted) != 0, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        await gate.WaitAsync(timeout.Token);
        try
        {
            if (Volatile.Read(ref disposalStarted) != 0) throw new OperationCanceledException("Connection is closing.", lifetime.Token);
            RequireSynchronizedConsole(); timeout.Token.ThrowIfCancellationRequested();
            var transcript = new List<string>();
            async Task<GrblConsoleResult> Exchange(string command)
            {
                transcript.Add("TX > " + command);
                var result = await ExchangeConsoleCore(GrblConsoleCommand.Prepare(command, true), timeout.Token);
                transcript.AddRange(result.Lines.Select(l => "RX < " + l));
                if (result.Outcome is GrblConsoleOutcome.Rejected or GrblConsoleOutcome.Alarm) throw new InvalidDataException("Controller rejected manual control: " + result.Lines[^1]);
                return result;
            }
            if (request.Action == GrblManualAction.CancelJog)
            {
                Volatile.Write(ref consoleUncertain, 1); await transport.WriteAsync("\u0085", timeout.Token); Volatile.Write(ref consoleUncertain, 0);
                transcript.Add("TX > jog cancel 0x85 (no acknowledgement)");
                return new(transcript.AsReadOnly(), "Jog cancellation requested; use Read status to confirm Idle. Not an emergency stop.");
            }
            if (request.Action == GrblManualAction.LaserOff)
            {
                await Exchange("M5 S0"); return new(transcript.AsReadOnly(), "Laser-off acknowledged. This is a queued command, not an emergency stop.");
            }
            var state = await Exchange("?"); var status = GrblStatus.Parse(state.Lines.Last(l => l.StartsWith('<')));
            if (status.State != "Idle") throw new InvalidDataException("Manual test requires a fresh Idle status; reported " + status.State + ".");
            await Exchange("M5 S0");
            string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
            if (request.Action is GrblManualAction.JogX or GrblManualAction.JogY)
            {
                var axis = request.Action == GrblManualAction.JogX ? "X" : "Y";
                await Exchange("$J=G21 G91 " + axis + N(request.Distance) + " F" + N(request.Feed));
                return new(transcript.AsReadOnly(), "Jog acknowledged, not completed. Read status/observe motion; no bed-position or homing guarantee. Laser left disabled.");
            }
            var report = await Exchange("$$"); var settings = new Dictionary<int, string>();
            foreach (var line in report.Lines.Where(l => l.StartsWith('$')))
            {
                var setting = GrblSetting.Parse(line); if (!settings.TryAdd(setting.Number, setting.Value)) throw new InvalidDataException("Duplicate firmware setting.");
            }
            double Setting(int id) => settings.TryGetValue(id, out var raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : throw new InvalidDataException("Missing/invalid firmware setting $" + id);
            if (Setting(32) != 1 || Setting(31) != 0 || Setting(30) != profile.MaxPower) throw new InvalidDataException("Pulse requires $32=1, $31=0 and reported $30 matching profile S maximum. Review settings/profile; nothing is changed automatically.");
            var power = (int)Math.Floor(profile.MaxPower * request.PowerPercent / 100);
            if (power < 1) throw new InvalidDataException("Requested power rounds to zero at this S maximum.");
            // One bounded write queues off before waiting for an acknowledgement. This is not a transport/hardware watchdog.
            var batch = "G1 G94 F60 M3 S" + power.ToString(CultureInfo.InvariantCulture) + "\nG4 P" + N(request.Milliseconds / 1000d) + "\nM5 S0\n";
            Volatile.Write(ref consoleUncertain, 1); await transport.WriteAsync(batch, timeout.Token); transcript.Add("TX > " + batch.Replace("\n", " | "));
            int terminals = 0; bool rejected = false;
            for (int i = 0; i < 256 && terminals < 3; i++)
            {
                var line = await transport.ReadLineAsync(timeout.Token); if (line.Length > 2048 || line.StartsWith("Grbl", StringComparison.OrdinalIgnoreCase) || line.StartsWith("ALARM:", StringComparison.Ordinal)) throw new IOException("Pulse exchange interrupted: " + line);
                transcript.Add("RX < " + line);
                if (line == "ok" || line.StartsWith("error:", StringComparison.Ordinal)) { terminals++; rejected |= line != "ok"; }
            }
            if (terminals != 3) throw new InvalidDataException("Incomplete pulse replies; reconnect required.");
            Volatile.Write(ref consoleUncertain, rejected ? 1 : 0);
            if (rejected) throw new InvalidDataException("A pulse command was rejected; off was queued, but verify controller/output. No retry.");
            return new(transcript.AsReadOnly(), "Pulse sequence acknowledged including M5 S0; nominal dwell only, not measured optical duration. G1/G94 and F60 remain active. No physical verification implied.");
        }
        finally { gate.Release(); }
    }
}
