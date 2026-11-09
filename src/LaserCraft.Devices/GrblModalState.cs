using System.Collections.Immutable;
using System.Text.RegularExpressions;

namespace LaserCraft.Devices;

public sealed record GrblModalState(string Raw, ImmutableArray<string> Words)
{
    public string? Units => OneOf("G20", "G21");
    public string? Distance => OneOf("G90", "G91");
    public string? Spindle => OneOf("M3", "M4", "M5");
    public string? FeedMode => OneOf("G93", "G94");
    private string? OneOf(params string[] group) => Words.FirstOrDefault(group.Contains);
    public static GrblModalState Parse(string line)
    {
        if (line.Length > 2048 || !line.StartsWith("[GC:", StringComparison.Ordinal) || !line.EndsWith(']')) throw new InvalidDataException("Invalid GRBL parser-state report.");
        var words = line[4..^1].Split(' ', StringSplitOptions.RemoveEmptyEntries).ToImmutableArray();
        if (words.Length == 0 || words.Any(w => !Regex.IsMatch(w, "^[A-Z][0-9]+(?:\\.[0-9]+)?$", RegexOptions.CultureInvariant))) throw new InvalidDataException("Malformed GRBL modal word.");
        if (words.Distinct(StringComparer.Ordinal).Count() != words.Length) throw new InvalidDataException("Duplicate GRBL modal word.");
        foreach (var group in new[] { new[] { "G20", "G21" }, new[] { "G90", "G91" }, new[] { "M3", "M4", "M5" }, new[] { "G93", "G94" } })
            if (words.Count(group.Contains) > 1) throw new InvalidDataException("Conflicting GRBL modal words.");
        foreach (var prefix in new[] { 'S', 'F', 'T' }) if (words.Count(w => w[0] == prefix) > 1) throw new InvalidDataException("Duplicate GRBL value word.");
        return new(line, words);
    }
}
