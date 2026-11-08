namespace LaserCraft.Domain;

public sealed record PathCompositionResult(DesignDocument Document, IReadOnlyList<Guid> Selection);

public static class PathComposition
{
    internal static List<DesignShape> SelectedPaths(DesignDocument document, IReadOnlySet<Guid> selection)
    {
        document.Validate();
        var selected = document.Shapes.Where(s => selection.Contains(s.Id)).ToList();
        if (selected.Count == 0 || selected.Count != selection.Count || selected.Any(s => s.Kind != ShapeKind.Path))
            throw new InvalidDataException("Select path objects only. Convert shapes to paths first; trace images before path operations.");
        return selected;
    }
    public static PathCompositionResult Separate(DesignDocument document, IReadOnlySet<Guid> selection)
    {
        var selected = SelectedPaths(document, selection); var replacements = new Dictionary<Guid, List<DesignShape>>(); var ids = new List<Guid>();
        foreach (var shape in selected)
        {
            if (shape.Path!.Contours.Length == 1) { ids.Add(shape.Id); continue; }
            var contours = VectorPaths.World(shape); var pieces = new List<DesignShape>();
            for (int i = 0; i < contours.Length; i++)
            {
                // A degenerate contour can be valid inside a compound object's nonzero frame,
                // but cannot be represented as a standalone zero-extent object. Reject atomically.
                var piece = VectorPaths.Create([contours[i]], shape.Layer, i == 0 ? shape.Id : null); pieces.Add(piece); ids.Add(piece.Id);
            }
            replacements[shape.Id] = pieces;
        }
        if (replacements.Count == 0) throw new InvalidDataException("The selected paths already have one contour each.");
        var shapes = new List<DesignShape>();
        foreach (var shape in document.Shapes) { if (replacements.TryGetValue(shape.Id, out var pieces)) shapes.AddRange(pieces); else shapes.Add(shape); }
        var next = document with { Shapes = shapes }; next.Validate(); return new(next, ids.AsReadOnly());
    }
    public static PathCompositionResult Combine(DesignDocument document, IReadOnlySet<Guid> selection)
    {
        var selected = SelectedPaths(document, selection);
        if (selected.Count < 2) throw new InvalidDataException("Select at least two path objects to combine.");
        if (selected.Any(s => s.Layer != selected[0].Layer)) throw new InvalidDataException("Assign the selected paths to the same layer before combining so their cut settings are preserved.");
        var combined = VectorPaths.Create(selected.SelectMany(s => VectorPaths.World(s)), selected[0].Layer, selected[0].Id);
        var shapes = new List<DesignShape>();
        foreach (var shape in document.Shapes)
        {
            if (shape.Id == combined.Id) shapes.Add(combined);
            else if (!selection.Contains(shape.Id)) shapes.Add(shape);
        }
        var next = document with { Shapes = shapes }; next.Validate(); return new(next, new[] { combined.Id });
    }
}
