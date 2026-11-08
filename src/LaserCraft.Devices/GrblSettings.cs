using System.Globalization;

namespace LaserCraft.Devices;

public sealed record GrblSetting(int Number, string Value, string Raw)
{
    public static GrblSetting Parse(string line)
    {
        var equals = line.IndexOf('=');
        if (line.Length > 2048 || equals < 2 || line[0] != '$' ||
            !int.TryParse(line.AsSpan(1, equals - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var number) ||
            equals == line.Length - 1 || line.Any(char.IsControl)) throw new InvalidDataException("Invalid GRBL setting report.");
        return new(number, line[(equals + 1)..], line);
    }
    public string Description => Number switch
    {
        10 => "Status report mask", 13 => "Report inches", 20 => "Soft limits", 21 => "Hard limits",
        22 => "Homing enabled", 30 => "Maximum spindle / laser S value", 31 => "Minimum spindle / laser S value",
        32 => "Laser mode", 100 => "X steps per mm", 101 => "Y steps per mm", 102 => "Z steps per mm",
        110 => "X maximum rate", 111 => "Y maximum rate", 112 => "Z maximum rate",
        130 => "X maximum travel", 131 => "Y maximum travel", 132 => "Z maximum travel", _ => "Firmware setting"
    };
}
