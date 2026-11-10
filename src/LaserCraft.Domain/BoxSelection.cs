namespace LaserCraft.Domain;

public static class BoxSelection
{
    public static HashSet<Guid> Select(DesignDocument document, IReadOnlySet<Guid> current, BoundsMm box, bool crossing, bool additive = false, bool individual = false)
    {
        if (!double.IsFinite(box.X) || !double.IsFinite(box.Y) || !double.IsFinite(box.Width) || !double.IsFinite(box.Height) || box.Width < 0 || box.Height < 0 || !double.IsFinite(box.X + box.Width) || !double.IsFinite(box.Y + box.Height)) throw new InvalidDataException("Invalid selection box.");
        var valid = document.Shapes.Select(s => s.Id).ToHashSet();
        var result = additive ? current.Where(valid.Contains).ToHashSet() : [];
        bool Matches(BoundsMm b) => crossing
            ? b.X <= box.X + box.Width && b.X + b.Width >= box.X && b.Y <= box.Y + box.Height && b.Y + b.Height >= box.Y
            : b.X >= box.X && b.Y >= box.Y && b.X + b.Width <= box.X + box.Width && b.Y + b.Height <= box.Y + box.Height;
        var visible = document.Shapes.Where(s => document.Layers[s.Layer].Visible).Select(s => s.Id).ToHashSet();
        var candidates = individual ? visible : ShapeGroups.Expand(document, visible);
        var units = individual ? document.Shapes.Where(s => candidates.Contains(s.Id)).Select(s => new ShapeUnit(Array.AsReadOnly(new[] { s }), ShapeEditing.Bounds(new[] { s }))).ToArray() : ShapeGroups.Units(document, candidates);
        foreach (var unit in units)
        {
            var hit = crossing ? unit.Members.Any(s => visible.Contains(s.Id) && Matches(new(s.X, s.Y, s.Width, s.Height))) : Matches(unit.Bounds);
            if (hit) result.UnionWith(unit.Members.Select(s => s.Id));
        }
        return result;
    }
}
