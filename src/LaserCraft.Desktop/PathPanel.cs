using LaserCraft.Domain;
using System.Collections.Immutable;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private void ConvertToPaths()
    {
        var c = Current.Canvas; if (c.Selection.Count == 0) { status.Text = "Select vector artwork first."; return; }
        try { c.Apply(ShapeEditing.Transform(c.History.Document, c.Selection, VectorPaths.Convert)); SetTool("Nodes"); }
        catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
    private void ClosePaths()
    {
        var c = Current.Canvas;
        try { c.Apply(ShapeEditing.Transform(c.History.Document, c.Selection, s => s.Kind == ShapeKind.Path ? s with { Path = new(s.Path!.Contours.Select(p => p with { Closed = true }).ToImmutableArray()) } : s)); }
        catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
}
