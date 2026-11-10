namespace LaserCraft.Domain;

public sealed record ShapeUnit(IReadOnlyList<DesignShape> Members, BoundsMm Bounds);
public sealed record GroupEditResult(DesignDocument Document, IReadOnlySet<Guid> Selection);
public static class ShapeGroups
{
    public static HashSet<Guid> Expand(DesignDocument d, IReadOnlySet<Guid> ids)
    {
        var selected = d.Shapes.Where(s => ids.Contains(s.Id)).ToArray();
        if (selected.Length != ids.Count) throw new InvalidDataException("Selection contains objects no longer in this document.");
        var groups = selected.Where(s => s.GroupId is not null).Select(s => s.GroupId!.Value).ToHashSet();
        return d.Shapes.Where(s => ids.Contains(s.Id) || s.GroupId is Guid group && groups.Contains(group)).Select(s => s.Id).ToHashSet();
    }
    public static HashSet<Guid> Select(DesignDocument d, IReadOnlySet<Guid> current, Guid? hit, bool additive, bool individual = false)
    {
        var valid = d.Shapes.Select(s => s.Id).ToHashSet(); var selected = current.Where(valid.Contains).ToHashSet();
        if (hit is null) return additive ? selected : [];
        if (!valid.Contains(hit.Value)) throw new InvalidDataException("Clicked object is no longer available.");
        var unit = individual ? new HashSet<Guid> { hit.Value } : Expand(d, new HashSet<Guid> { hit.Value });
        if (!additive) return !individual && unit.IsSubsetOf(selected) ? selected : unit;
        if (unit.IsSubsetOf(selected)) selected.ExceptWith(unit); else selected.UnionWith(unit);
        return selected;
    }
    public static IReadOnlyList<ShapeUnit> Units(DesignDocument d, IReadOnlySet<Guid> ids)
    {
        var selected = d.Shapes.Where(s => ids.Contains(s.Id)).ToArray();
        if (selected.Length != ids.Count) throw new InvalidDataException("Selection contains objects no longer in this document.");
        // Group and object identities occupy separate namespaces.
        return selected.GroupBy(s => (Grouped: s.GroupId is not null, Id: s.GroupId ?? s.Id)).Select(g => { var members = Array.AsReadOnly(g.ToArray()); return new ShapeUnit(members, ShapeEditing.Bounds(members)); }).ToArray();
    }
    public static GroupEditResult Group(DesignDocument d, IReadOnlySet<Guid> ids)
    {
        d.Validate(); var expanded = Expand(d, ids);
        if (expanded.Count < 2) throw new InvalidDataException("Select at least two objects to group.");
        var selected = d.Shapes.Where(s => expanded.Contains(s.Id)).ToArray();
        if (selected[0].GroupId is Guid group && selected.All(s => s.GroupId == group)) throw new InvalidDataException("The selection is already one group.");
        var id = Guid.NewGuid(); var next = d with { Shapes = d.Shapes.Select(s => expanded.Contains(s.Id) ? s with { GroupId = id } : s).ToList() }; next.Validate(); return new(next, expanded);
    }
    public static GroupEditResult Ungroup(DesignDocument d, IReadOnlySet<Guid> ids)
    {
        d.Validate(); var expanded = Expand(d, ids); var selectedGroups = d.Shapes.Where(s => ids.Contains(s.Id) && s.GroupId is not null).Select(s => s.GroupId!.Value).ToHashSet();
        if (selectedGroups.Count == 0) throw new InvalidDataException("Select grouped artwork to ungroup.");
        var next = d with { Shapes = d.Shapes.Select(s => s.GroupId is Guid group && selectedGroups.Contains(group) ? s with { GroupId = null } : s).ToList() }; next.Validate(); return new(next, expanded);
    }
    public static List<DesignShape> RemapCopies(IEnumerable<DesignShape> copies)
    {
        var groups = new Dictionary<Guid, Guid>();
        return copies.Select(s =>
        {
            if (s.GroupId is not Guid old) return s;
            if (!groups.TryGetValue(old, out var next)) groups.Add(old, next = Guid.NewGuid());
            return s with { GroupId = next };
        }).ToList();
    }
}
