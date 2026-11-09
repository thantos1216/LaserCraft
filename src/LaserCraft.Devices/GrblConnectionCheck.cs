using System.Globalization;
using LaserCraft.Domain;

namespace LaserCraft.Devices;

public enum CheckFinding { Match, Attention, Unknown }
public sealed record ConnectionCheckItem(string Name, CheckFinding Finding, string Detail);
public static class GrblConnectionCheck
{
    // A diagnostic comparison only. It never grants execution authority or establishes homing.
    public static IReadOnlyList<ConnectionCheckItem> Compare(DeviceProfile profile, IReadOnlyList<GrblSetting> settings, GrblModalState modal, GrblStatus status)
    {
        profile.Validate(); var result = new List<ConnectionCheckItem>();
        double? Value(int number)
        {
            var matches = settings.Where(s => s.Number == number).ToArray();
            if (matches.Length > 1) throw new InvalidDataException("Duplicate setting in connection check.");
            return matches.Length == 1 && double.TryParse(matches[0].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) && double.IsFinite(v) ? v : null;
        }
        void Setting(string name, int number, double expected)
        {
            var value = Value(number); result.Add(new(name, value is null ? CheckFinding.Unknown : value == expected ? CheckFinding.Match : CheckFinding.Attention,
                $"Reported ${number}: {settings.FirstOrDefault(s => s.Number == number)?.Value ?? "not reported"}; profile/expected: {expected.ToString(CultureInfo.InvariantCulture)}"));
        }
        Setting("S maximum", 30, profile.MaxPower); Setting("Laser mode", 32, 1); Setting("Status reporting in millimetres", 13, 0);
        void Mode(string name, string? value, string expected) => result.Add(new(name, value is null ? CheckFinding.Unknown : value == expected ? CheckFinding.Match : CheckFinding.Attention, "Reported: " + (value ?? "not reported") + "; expected: " + expected));
        Mode("G-code units", modal.Units, "G21"); Mode("Distance mode", modal.Distance, "G90"); Mode("Feed mode", modal.FeedMode, "G94"); Mode("Spindle mode", modal.Spindle, "M5");
        result.Add(new("Controller state", status.State == "Idle" ? CheckFinding.Match : CheckFinding.Attention, status.State));
        result.Add(new("Reported spindle/S", status.Spindle is null ? CheckFinding.Unknown : status.Spindle == 0 ? CheckFinding.Match : CheckFinding.Attention, status.Spindle?.ToString(CultureInfo.InvariantCulture) ?? "not reported"));
        foreach (var axis in new[] { ("X", 130, profile.BedWidth), ("Y", 131, profile.BedHeight) })
        {
            var travel = Value(axis.Item2); result.Add(new(axis.Item1 + " travel vs bed", travel is null ? CheckFinding.Unknown : travel <= 0 || travel < axis.Item3 ? CheckFinding.Attention : CheckFinding.Match,
                FormattableString.Invariant($"Reported travel: {travel?.ToString(CultureInfo.InvariantCulture) ?? "not reported"}; profile bed: {axis.Item3} mm. Travel is not proof of usable area or origin.")));
        }
        result.Add(new("Position / homing", CheckFinding.Unknown, FormattableString.Invariant($"{status.Coordinates}: X {status.X?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}, Y {status.Y?.ToString(CultureInfo.InvariantCulture) ?? "unknown"}. Homing, work zero, offsets and physical bounds have not been established.")));
        return result.AsReadOnly();
    }
}
