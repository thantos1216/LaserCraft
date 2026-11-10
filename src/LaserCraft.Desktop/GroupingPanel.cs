using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private void ChangeGrouping(bool ungroup)
    {
        var canvas = Current.Canvas;
        try
        {
            var result = ungroup ? ShapeGroups.Ungroup(canvas.History.Document, canvas.Selection) : ShapeGroups.Group(canvas.History.Document, canvas.Selection);
            canvas.CancelDrawing(); canvas.Selection.Clear(); canvas.Selection.UnionWith(result.Selection); canvas.Apply(result.Document); SetTool("Select");
            status.Text = ungroup ? "Ungrouped · members keep their layers/settings · one Undo step." : "Grouped · click selects all members, including hidden layers; Alt-click selects an individual member · one Undo step.";
        }
        catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
}
