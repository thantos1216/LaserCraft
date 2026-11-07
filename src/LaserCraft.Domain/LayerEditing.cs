namespace LaserCraft.Domain;

public static class LayerEditing
{
    public static DesignDocument Add(DesignDocument d, string name, LayerKind kind, string? color = null)
    {
        var layer = new CutLayer(name, color ?? LayerPalette.Colors[d.Layers.Count % LayerPalette.Colors.Length], Mode: kind == LayerKind.Image ? CutMode.Image : CutMode.Line, Kind: kind);
        var result = d with { Layers = [.. d.Layers, layer] }; result.Validate(); return result;
    }
    public static DesignDocument Move(DesignDocument d, int from, int to)
    {
        if (from < 0 || to < 0 || from >= d.Layers.Count || to >= d.Layers.Count) throw new ArgumentOutOfRangeException(nameof(to));
        var order = Enumerable.Range(0, d.Layers.Count).ToList(); var item = order[from]; order.RemoveAt(from); order.Insert(to, item);
        var indices = order.Select((old, index) => (old, index)).ToDictionary(p => p.old, p => p.index);
        var result = d with { Layers = order.Select(i => d.Layers[i]).ToList(), Shapes = d.Shapes.Select(s => s with { Layer = indices[s.Layer] }).ToList() }; result.Validate(); return result;
    }
    public static DesignDocument Assign(DesignDocument d, IReadOnlySet<Guid> ids, int layer)
    {
        if (layer < 0 || layer >= d.Layers.Count) throw new ArgumentOutOfRangeException(nameof(layer));
        return ShapeEditing.Transform(d, ids, s => s with { Layer = layer });
    }
    public static DesignDocument RemoveEmpty(DesignDocument d, int index)
    {
        if (index < 0 || index >= d.Layers.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (d.Layers.Count == 1 || d.Shapes.Any(s => s.Layer == index)) throw new InvalidDataException("Only empty layers can be deleted, and one layer must remain. Reassign or delete its objects first.");
        var result = d with { Layers = d.Layers.Where((_, i) => i != index).ToList(), Shapes = d.Shapes.Select(s => s.Layer > index ? s with { Layer = s.Layer - 1 } : s).ToList() }; result.Validate(); return result;
    }
}
