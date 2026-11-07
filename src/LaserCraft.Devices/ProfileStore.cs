using LaserCraft.Domain;
using System.Text.Json;
namespace LaserCraft.Devices;

public static class ProfileStore
{
    public static List<DeviceProfile> Load(string path)
    {
        if (!File.Exists(path)) return [DeviceProfile.Simulator()];
        if (new FileInfo(path).Length > 1_000_000) throw new InvalidDataException("Device profile file is too large.");
        var profiles = JsonSerializer.Deserialize<List<DeviceProfile>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty device profiles.");
        Validate(profiles); return profiles;
    }
    public static void Save(string path, IReadOnlyList<DeviceProfile> profiles)
    {
        Validate(profiles); var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(profiles, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, full, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static void Validate(IReadOnlyList<DeviceProfile> p) { if (p.Count < 1 || p.Count > 100 || p.Any(x => x is null) || p.Select(x => x.Id).Distinct().Count() != p.Count) throw new InvalidDataException("Invalid device profile list."); foreach (var profile in p) profile.Validate(); }
}
