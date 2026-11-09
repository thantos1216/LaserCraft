using System.Globalization;
using LaserCraft.Domain;

namespace LaserCraft.Devices;

/// <summary>Manual-control rehearsal only. Has no transport, endpoint or hardware write API.</summary>
public sealed class ManualControlSimulation
{
    private readonly Queue<string> commands = new();
    public DeviceProfile Profile { get; }
    public PointMm Position { get; private set; }
    public int SValue { get; private set; }
    private TimeSpan remaining;
    public double RemainingPulseSeconds => remaining.TotalSeconds;
    public bool IsPulsing => SValue > 0;
    public int DroppedCommands { get; private set; }
    public IReadOnlyList<string> Commands => commands.ToArray();
    public ManualControlSimulation(DeviceProfile profile)
    {
        profile.Validate();
        if (profile.Connection != ConnectionKind.Simulator) throw new InvalidDataException("Manual rehearsal requires a Simulator profile. Hardware control is not implemented.");
        Profile = profile;
    }
    public void Jog(double dx, double dy, double feed)
    {
        if (IsPulsing) throw new InvalidDataException("Stop the simulated pulse before jogging.");
        if (!double.IsFinite(dx) || !double.IsFinite(dy) || !double.IsFinite(feed) || feed <= 0 || feed > 100000 || (dx == 0 && dy == 0)) throw new InvalidDataException("Jog needs finite movement and a feed between 0 and 100000 mm/min.");
        var next = new PointMm(Position.X + dx, Position.Y + dy);
        if (next.X < 0 || next.Y < 0 || next.X > Profile.BedWidth || next.Y > Profile.BedHeight) throw new InvalidDataException("Simulated jog would leave the configured bed.");
        Record("M5"); Record($"$J=G21 G91 X{N(dx)} Y{N(dy)} F{N(feed)}"); Position = next;
    }
    public void StartPulse(double percent, int milliseconds)
    {
        if (IsPulsing) throw new InvalidDataException("A simulated pulse is already active.");
        if (!double.IsFinite(percent) || percent <= 0 || percent > 5 || milliseconds < 1 || milliseconds > 500) throw new InvalidDataException("Rehearsal pulse must be above 0 and at most 5%, for 1–500 ms.");
        var power = (int)Math.Floor(Profile.MaxPower * percent / 100);
        if (power == 0) throw new InvalidDataException("Requested power is below one S-value step for this profile.");
        SValue = power; remaining = TimeSpan.FromMilliseconds(milliseconds);
        Record($"M3 S{power.ToString(CultureInfo.InvariantCulture)}"); Record($"G4 P{N(RemainingPulseSeconds)}");
    }
    public void Advance(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(elapsed));
        if (!IsPulsing) return;
        remaining = elapsed >= remaining ? TimeSpan.Zero : remaining - elapsed; if (remaining == TimeSpan.Zero) Stop();
    }
    public void Stop() { SValue = 0; remaining = TimeSpan.Zero; Record("M5"); }
    private void Record(string command) { if (commands.Count == 256) { commands.Dequeue(); DroppedCommands++; } commands.Enqueue(command); }
    private static string N(double value) => value.ToString("0.#################", CultureInfo.InvariantCulture);
}
