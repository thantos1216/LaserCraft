using Avalonia.Controls;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private void InstallCanvasMenu(Session session)
    {
        var canvas = session.Canvas;
        var menu = new ContextMenu();
        canvas.ContextMenu = menu;
        canvas.ArtworkMenuRequested += () =>
        {
            if (Current != session || canvas.ShowPreview) return;
            var selected = canvas.History.Document.Shapes.Where(s => canvas.Selection.Contains(s.Id)).ToArray();
            bool any = selected.Length > 0;
            bool paths = any && selected.All(s => s.Kind == ShapeKind.Path);
            bool vectors = any && selected.All(s => s.Kind != ShapeKind.Image);
            MenuItem Action(string label, System.Action action, bool enabled = true)
            {
                var item = Item(label, () => { if (Current == session) action(); });
                item.IsEnabled = enabled; return item;
            }
            MenuItem Async(string label, Func<Task> action, bool enabled = true)
            {
                var item = AsyncItem(label, async () => { if (Current == session) await action(); });
                item.IsEnabled = enabled; return item;
            }
            menu.ItemsSource = new Control[]
            {
                Async("Import image…", ImportImage), Async("Import SVG…", ImportSvg), Async("Create text…", () => EditText(false)),
                new Separator(),
                MenuGroup("Edit artwork", Async("Edit text…", () => EditText(true), selected.Length == 1 && selected[0].Text is not null),
                Async("Image preparation…", EditImageSettings, selected.Length == 1 && selected[0].Kind == ShapeKind.Image),
                Async("Trace image…", TraceImage, selected.Length == 1 && selected[0].Kind == ShapeKind.Image),
                Action("Edit path nodes", () => SetTool("Nodes"), paths)),
                new Separator(),
                Action("Copy", Copy, any), Action("Cut", () => { Copy(); canvas.DeleteSelected(); }, any),
                Action("Paste", Paste, clipboard.Length > 0), Action("Duplicate", Duplicate, any), Action("Delete", canvas.DeleteSelected, any),
                new Separator(),
                Action("Group", () => ChangeGrouping(false), selected.Length > 1),
                Action("Ungroup", () => ChangeGrouping(true), selected.Any(s => s.GroupId is not null)),
                MenuGroup("Transform", Action("Mirror horizontally", () => Mirror(true), any), Action("Mirror vertically", () => Mirror(false), any),
                Action("Center on bed", CenterOnBed, any)),
                new Separator(),
                MenuGroup("Combine / separate paths", Action("Convert to paths", ConvertToPaths, vectors),
                Action("Combine contours (same-layer paths)", () => ComposePaths(false), paths && selected.Length > 1 && selected.All(s => s.Layer == selected[0].Layer)),
                Action("Separate contours", () => ComposePaths(true), paths && selected.Any(s => s.Path!.Contours.Length > 1)),
                Async("Join open endpoints…", JoinPaths, paths)),
                new Separator(),
                Action("Select all", canvas.SelectAll), Action("Clear selection", () => { canvas.Selected = null; canvas.Refresh(); }, any),
                Action("Undo", Undo), Action("Redo", Redo)
            };
            menu.Open(canvas);
        };
    }
}
