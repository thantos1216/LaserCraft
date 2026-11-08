using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private async Task JoinPaths()
    {
        var session = Current; var canvas = session.Canvas; var document = canvas.History.Document; var fingerprint = Snapshot(session); var scope = canvas.Selection.ToHashSet();
        var sources = document.Shapes.Where(s => scope.Contains(s.Id)).ToArray();
        if (sources.Length == 0 || sources.Any(s => s.Kind != ShapeKind.Path)) { status.Text = "Select two open path contours. Convert shapes to paths or trace images first."; return; }
        var dialog = new Window { Title = "Join open path endpoints", Width = 780, Height = 640, MinWidth = 620, MinHeight = 480, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background, Foreground = Foreground };
        var maximum = Number(0, 20000); maximum.Value = .1m; maximum.Increment = .1m;
        var method = new ComboBox { ItemsSource = new[] { "Merge endpoints at midpoint", "Bridge gap with a cut segment" }, SelectedIndex = 0, MinWidth = 270 };
        var preview = new JoinPreview(sources); var info = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(3, 10) }; PathJoinResult? prepared = null;
        Button apply = null!;
        apply = Button("Join paths", () =>
        {
            if (prepared is null) return;
            if (!sessions.Contains(session) || Current != session || Snapshot(session) != fingerprint || !canvas.Selection.SetEquals(scope)) { info.Text = "The source document or selection changed. Close and reopen joining."; prepared = null; apply.IsEnabled = false; return; }
            canvas.CancelDrawing(); canvas.ActiveLayer = prepared.Document.Shapes.First(s => s.Id == prepared.JoinedId).Layer; canvas.Selected = prepared.JoinedId;
            canvas.Apply(prepared.Document); SetTool("Nodes"); dialog.Close(); status.Text = "Joined two open contours · one undo step. Inspect Preview before output.";
        });
        void Update()
        {
            prepared = null; preview.Joined = null; apply.IsEnabled = false;
            try
            {
                var next = PathJoining.Prepare(document, scope, (double)(maximum.Value ?? 0), (PathJoinMode)method.SelectedIndex);
                prepared = next; preview.Joined = next.Document.Shapes.First(s => s.Id == next.JoinedId); apply.IsEnabled = true;
                info.Text = $"Nearest endpoint gap: {next.Gap:G8} mm. " + (next.Mode == PathJoinMode.MergeAtMidpoint
                    ? $"Both endpoints move {next.Gap / 2:G8} mm to their midpoint; adjacent segments change."
                    : $"Original endpoints stay fixed; {next.Gap:G8} mm of laser-on connecting path is added.") + " The result remains open and retains the first object's layer. Cyan: proposed path; dashed gray: originals; orange: original endpoints. Nothing changes until Join paths.";
                preview.EndA = next.EndA; preview.EndB = next.EndB;
            }
            catch (InvalidDataException ex) { info.Text = ex.Message; preview.EndA = preview.EndB = null; }
            preview.InvalidateVisual();
        }
        maximum.ValueChanged += (_, _) => Update(); method.SelectionChanged += (_, _) => Update();
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,*,Auto,Auto"), Margin = new Thickness(16) };
        root.Children.Add(Label("Join exactly two open contours · closest endpoints are chosen", 17));
        var controls = new WrapPanel(); Inline(controls, "Maximum gap · mm", maximum, 125); controls.Children.Add(method); Grid.SetRow(controls, 1); root.Children.Add(controls);
        Grid.SetRow(preview, 2); root.Children.Add(preview); Grid.SetRow(info, 3); root.Children.Add(info);
        var actions = new WrapPanel(); actions.Children.Add(apply); actions.Children.Add(Button("Cancel", () => dialog.Close())); Grid.SetRow(actions, 4); root.Children.Add(actions);
        dialog.Content = root; Update(); await dialog.ShowDialog(this);
    }
}

internal sealed class JoinPreview(IReadOnlyList<DesignShape> sources) : Control
{
    public DesignShape? Joined { get; set; }
    public PointMm? EndA { get; set; }
    public PointMm? EndB { get; set; }
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0A111B")), new Rect(Bounds.Size));
        var points = sources.SelectMany(s => VectorPaths.World(s).SelectMany(c => c.Points)).ToArray(); if (points.Length == 0) return;
        var x = points.Min(p => p.X); var y = points.Min(p => p.Y); var width = points.Max(p => p.X) - x; var height = points.Max(p => p.Y) - y;
        var scale = Math.Max(.001, Math.Min(Math.Max(1, Bounds.Width - 40) / Math.Max(width, 1), Math.Max(1, Bounds.Height - 40) / Math.Max(height, 1)));
        Point Screen(PointMm p) => new(Bounds.Width / 2 + (p.X - x - width / 2) * scale, Bounds.Height / 2 + (p.Y - y - height / 2) * scale);
        var originalPen = new Pen(Brushes.SlateGray, 3, DashStyle.Dash); var joinedPen = new Pen(Brushes.Cyan, 1.5); var endpointPen = new Pen(Brushes.Orange, 2);
        foreach (var source in sources) foreach (var edge in VectorPaths.Edges(source)) context.DrawLine(originalPen, Screen(edge.From), Screen(edge.To));
        if (Joined is { } joined) foreach (var edge in VectorPaths.Edges(joined)) context.DrawLine(joinedPen, Screen(edge.From), Screen(edge.To));
        foreach (var p in new[] { EndA, EndB }) if (p is PointMm point) context.DrawEllipse(null, endpointPen, Screen(point), 5, 5);
    }
}
