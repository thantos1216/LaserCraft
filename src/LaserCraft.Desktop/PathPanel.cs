using LaserCraft.Domain;
using System.Collections.Immutable;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private void EditPathNode(string operation) => status.Text = Current.Canvas.EditNode(operation);
    private void ComposePaths(bool separate)
    {
        var canvas = Current.Canvas;
        try
        {
            var result = separate ? PathComposition.Separate(canvas.History.Document, canvas.Selection) : PathComposition.Combine(canvas.History.Document, canvas.Selection);
            canvas.CancelDrawing(); canvas.Selected = null; foreach (var id in result.Selection) canvas.Selection.Add(id);
            canvas.Apply(result.Document);
            status.Text = separate ? $"Separated into {result.Selection.Count} selected path objects. Ctrl-click to select individual regions and assign layers." : "Combined contours into one path. Endpoints remain unjoined; Undo restores the original objects.";
        }
        catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
    private void ConvertToPaths()
    {
        var c = Current.Canvas; if (c.Selection.Count == 0) { status.Text = "Select vector artwork first."; return; }
        try { c.Apply(ShapeEditing.Transform(c.History.Document, c.Selection, VectorPaths.Convert)); SetTool("Nodes"); }
        catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
    private void ClosePaths()
    {
        var c = Current.Canvas;
        try { c.Apply(ShapeEditing.Transform(c.History.Document, c.Selection, s => s.Kind == ShapeKind.Path ? s with { Text = null, Path = new(s.Path!.Contours.Select(p => p with { Closed = true }).ToImmutableArray()) } : s)); }
        catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
}
