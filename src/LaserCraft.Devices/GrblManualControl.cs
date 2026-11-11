using System.Globalization;
using LaserCraft.Domain;

namespace LaserCraft.Devices;

public enum GrblManualAction { JogX, JogY, Pulse, LaserOff, CancelJog, CutLineX, CutLineY, Home }
public sealed record GrblManualRequest(GrblManualAction Action, double Distance = 1, double Feed = 600, double PowerPercent = 1, int Milliseconds = 100, bool ClearPathConfirmed = false)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Action)) throw new InvalidDataException("Unknown manual action.");
        if (Action is GrblManualAction.JogX or GrblManualAction.JogY && (!double.IsFinite(Distance) || Math.Abs(Distance) < .001 || Math.Abs(Distance) > 10000 || !double.IsFinite(Feed) || Feed < 60 || Feed > 3000)) throw new InvalidDataException("Physical jogs require 0.001–10000 mm and 60–3000 mm/min.");
        if (Action is GrblManualAction.CutLineX or GrblManualAction.CutLineY && (!double.IsFinite(Distance) || Math.Abs(Distance) < .1 || Math.Abs(Distance) > 20 || !double.IsFinite(Feed) || Feed < 60 || Feed > 3000 || !double.IsFinite(PowerPercent) || PowerPercent <= 0 || PowerPercent > 100)) throw new InvalidDataException("Line tests require 0.1–20 mm, 60–3000 mm/min and >0–100% power.");
        if (Action == GrblManualAction.Pulse && (!double.IsFinite(PowerPercent) || PowerPercent <= 0 || PowerPercent > 5 || Milliseconds < 1 || Milliseconds > 500)) throw new InvalidDataException("Physical pulses require >0–5% and 1–500 ms.");
    }
}
public sealed record GrblManualResult(IReadOnlyList<string> Lines, string Summary);

public sealed partial class GrblMonitor
{
    public async Task<GrblManualResult> ExecuteManualAsync(DeviceProfile profile, GrblManualRequest request, bool allowMotion, bool allowFire = false, CancellationToken cancellationToken = default)
    {
        profile.Validate(); request.Validate();
        if (request.Action is GrblManualAction.JogX or GrblManualAction.JogY && Math.Abs(request.Distance) > (request.Action == GrblManualAction.JogX ? profile.BedWidth : profile.BedHeight)) throw new InvalidDataException("Jog distance exceeds the selected axis profile travel; this does not establish current clearance.");
        if (request.Action is GrblManualAction.CutLineX or GrblManualAction.CutLineY && !request.ClearPathConfirmed) throw new InvalidDataException("Confirm positioning, clear signed path, protection and physical stop before a line test.");
        if (!allowMotion || request.Action is GrblManualAction.Pulse or GrblManualAction.CutLineX or GrblManualAction.CutLineY && !allowFire) throw new InvalidDataException("Enable physical control, and the separate firing permission for a pulse.");
        if (profile.Controller != ControllerFamily.Grbl || profile.Connection == ConnectionKind.Simulator || !Identity.StartsWith("[VER:1.1", StringComparison.Ordinal) && !Identity.StartsWith("Grbl 1.1", StringComparison.Ordinal)) throw new InvalidDataException("Guided physical controls require an identified GRBL 1.1 serial/TCP connection. Other firmware remains console-only.");
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposalStarted) != 0, this);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.Token); timeout.CancelAfter(TimeSpan.FromSeconds(request.Action == GrblManualAction.Home ? 120 : request.Action is GrblManualAction.CutLineX or GrblManualAction.CutLineY ? 40 : 10));
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
            if (request.Action == GrblManualAction.Home)
            {
                Volatile.Write(ref homingConfirmed,0); homedMachinePosition=null;userOrigin=null;
                var homeReport = await Exchange("$$");
                if (!HomingEnabled(homeReport.Lines.Where(l => l.StartsWith('$')).Select(GrblSetting.Parse))) throw new InvalidDataException("Firmware homing $22 is not reported enabled.");
                var before = await Exchange("?"); var homingState = GrblStatus.Parse(before.Lines.Last(l => l.StartsWith('<'))).State;
                if (homingState is not ("Idle" or "Alarm")) throw new InvalidDataException("Home requires Idle or Alarm; reported " + homingState);
                await Exchange("$H"); var after = await Exchange("?");
                var homeStatus=GrblStatus.Parse(after.Lines.Last(l => l.StartsWith('<')));
                if (homeStatus.State != "Idle") throw new IOException("Home did not finish Idle.");
                homedMachinePosition=homeStatus.Coordinates=="MPos" && homeStatus.X is double hx && homeStatus.Y is double hy ? new(hx,hy) : null;
                Volatile.Write(ref homingConfirmed,1);
                return new(transcript.AsReadOnly(), "Homing acknowledged and Idle reported. Firmware reference established; work offsets unchanged, displayed machine coordinates may be negative.");
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
            if (Setting(32) != 1 || Setting(31) != 0 || Setting(30) != profile.MaxPower) throw new InvalidDataException("Firing requires $32=1, $31=0 and reported $30 matching profile S maximum. Review settings/profile; nothing is changed automatically.");
            var power = (int)Math.Floor(profile.MaxPower * request.PowerPercent / 100);
            if (power < 1) throw new InvalidDataException("Requested power rounds to zero at this S maximum.");
            if (request.Action is GrblManualAction.CutLineX or GrblManualAction.CutLineY)
            {
                var modalReply = await Exchange("$G"); var reports = modalReply.Lines.Where(l => l.StartsWith("[GC:", StringComparison.Ordinal)).ToArray();
                if (reports.Length != 1) throw new InvalidDataException("Expected exactly one parser-state report; no cut was sent.");
                var modal = GrblModalState.Parse(reports[0]);
                if (modal.Units != "G21" || modal.Distance != "G90" || modal.FeedMode != "G94") throw new InvalidDataException("Line test requires reported G21/G90/G94. Review parser modes first; no cut was sent.");
                var axis = request.Action == GrblManualAction.CutLineX ? "X" : "Y";
                var program = "G21 G91 G94 M4 S0\nG1 " + axis + N(request.Distance) + " F" + N(request.Feed) + " S" + power.ToString(CultureInfo.InvariantCulture) + "\nM5 S0\nG90 G0\n";
                await ExchangeManualBatch(program, 4, transcript, timeout.Token);
                var completion = await Exchange("?"); var finalStatus = GrblStatus.Parse(completion.Lines.Last(l => l.StartsWith('<')));
                if (finalStatus.State != "Idle") { Volatile.Write(ref consoleUncertain, 1); throw new IOException("Line test did not report Idle after final off; inspect hardware before reconnecting."); }
                return new(transcript.AsReadOnly(), "Line sequence acknowledged and controller reported Idle. Verify the physical mark/cut. Ends G21/G90/G94/G0, M5 S0; test feed remains active. No through-cut or optical verification implied.");
            }
            // Queue off before awaiting replies; this is not a hardware watchdog.
            var batch = "G1 G94 F60 M3 S" + power.ToString(CultureInfo.InvariantCulture) + "\nG4 P" + N(request.Milliseconds / 1000d) + "\nM5 S0\n";
            await ExchangeManualBatch(batch, 3, transcript, timeout.Token);
            return new(transcript.AsReadOnly(), "Pulse sequence acknowledged including M5 S0; nominal dwell only, not measured optical duration. G1/G94 and F60 remain active. No physical verification implied.");
        }
        finally { gate.Release(); }
    }
    private async Task ExchangeManualBatch(string batch, int expectedReplies, List<string> transcript, CancellationToken token)
    {
        Volatile.Write(ref consoleUncertain, 1); await transport.WriteAsync(batch, token); transcript.Add("TX > " + batch.Replace("\n", " | "));
        int terminals = 0; bool rejected = false;
        for (int i = 0; i < 256 && terminals < expectedReplies; i++)
        {
            var line = await transport.ReadLineAsync(token); if (line.Length > 2048 || line.StartsWith("Grbl", StringComparison.OrdinalIgnoreCase) || line.StartsWith("ALARM:", StringComparison.Ordinal)) throw new IOException("Manual batch interrupted: " + line);
            transcript.Add("RX < " + line);
            if (line == "ok" || line.StartsWith("error:", StringComparison.Ordinal)) { terminals++; rejected |= line != "ok"; }
        }
        if (terminals != expectedReplies) throw new InvalidDataException("Incomplete manual replies; reconnect required.");
        Volatile.Write(ref consoleUncertain, rejected ? 1 : 0);
        if (rejected) throw new InvalidDataException("A manual command was rejected; off was queued, but verify controller/output. No retry.");
    }

}
