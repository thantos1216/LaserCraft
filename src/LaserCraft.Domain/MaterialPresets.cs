using System.Text.Json;
namespace LaserCraft.Domain;

public sealed record LaserLayerSettings(double Speed, double Power, CutMode Mode, int Passes, bool AirAssist, double Interval, LayerKind Kind, RasterSettings? Raster)
{
    public static LaserLayerSettings Capture(CutLayer layer) => new(layer.Speed, layer.Power, layer.Mode, layer.Passes, layer.AirAssist, layer.Interval, layer.Kind, layer.Raster);
    public CutLayer Apply(CutLayer target)
    {
        if (target.Kind != Kind) throw new InvalidDataException("Image and vector presets require matching layer types.");
        return target with { Speed = Speed, Power = Power, Mode = Mode, Passes = Passes, AirAssist = AirAssist, Interval = Interval, Raster = Raster };
    }
    public void Validate() => (DesignDocument.Empty() with { Layers = [Apply(new("Preset", "#000000", Kind: Kind))] }).Validate();
}
public sealed record MaterialPreset(Guid Id, string Name, string Notes, double? ThicknessMm, LaserLayerSettings Settings)
{
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || Name.Any(char.IsControl) || Notes is null || Notes.Length > 2000 || ThicknessMm is double t && (!double.IsFinite(t) || t <= 0 || t > 1000) || Settings is null) throw new InvalidDataException("Invalid material preset name, notes, thickness or settings.");
        Settings.Validate();
    }
    public DesignDocument Apply(DesignDocument document, int layer)
    {
        Validate(); document.Validate();
        if (layer < 0 || layer >= document.Layers.Count) throw new InvalidDataException("Choose an existing target layer.");
        var layers = document.Layers.ToList(); layers[layer] = Settings.Apply(layers[layer]); var next = document with { Layers = layers }; next.Validate(); return next;
    }
}
public sealed record MaterialLibrary(int Version, List<MaterialPreset> Presets)
{
    public static MaterialLibrary Empty() => new(1, []);
    public void Validate()
    {
        if (Version != 1 || Presets is null || Presets.Count > 500 || Presets.Any(p => p is null) || Presets.Select(p => p.Id).Distinct().Count() != Presets.Count) throw new InvalidDataException("Invalid material library version, entries or duplicate IDs.");
        foreach (var preset in Presets) preset.Validate();
    }
}
public static class MaterialLibraryFile
{
    private const int MaxBytes = 2_000_000;
    public static MaterialLibrary Load(string path)
    {
        if (!File.Exists(path)) return MaterialLibrary.Empty();
        using var file = File.OpenRead(path); if (file.Length > MaxBytes) throw new InvalidDataException("Material library exceeds 2 MB.");
        var library = JsonSerializer.Deserialize<MaterialLibrary>(file) ?? throw new InvalidDataException("Empty material library."); library.Validate(); return library;
    }
    public static void Save(string path, MaterialLibrary library)
    {
        library.Validate(); var bytes = JsonSerializer.SerializeToUtf8Bytes(library, new JsonSerializerOptions { WriteIndented = true });
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Material library exceeds 2 MB.");
        var full = Path.GetFullPath(path); Directory.CreateDirectory(Path.GetDirectoryName(full)!); var temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllBytes(temp, bytes); if (File.Exists(full)) File.Copy(full, full + ".bak", true); File.Move(temp, full, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
