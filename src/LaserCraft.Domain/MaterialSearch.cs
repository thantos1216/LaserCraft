namespace LaserCraft.Domain;

public static class MaterialSearch
{
    public static IReadOnlyList<MaterialPreset> Find(MaterialLibrary library,string? query,LayerKind? kind=null)
    {
        library.Validate();if(kind is LayerKind k&&!Enum.IsDefined(k))throw new InvalidDataException("Unknown material layer type.");
        var words=(query??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        return library.Presets.Where(p=>(kind is null||p.Settings.Kind==kind)&&words.All(w=>p.Name.Contains(w,StringComparison.OrdinalIgnoreCase)||p.Notes.Contains(w,StringComparison.OrdinalIgnoreCase)))
            .OrderBy(p=>p.Name,StringComparer.OrdinalIgnoreCase).ThenBy(p=>p.Id).ToArray();
    }
}
