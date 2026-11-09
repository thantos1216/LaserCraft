using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private async Task ImportSvg()
    {
        try
        {
            var session = Current;
            var files = await StorageProvider.OpenFilePickerAsync(new() { Title = "Import SVG geometry", FileTypeFilter = [new("SVG geometry") { Patterns = ["*.svg"] }] });
            if (files.Count == 0 || workspaceClosed || !sessions.Contains(session)) return;
            using var stream = await files[0].OpenReadAsync(); if (stream.Length > 4_000_000) throw new InvalidDataException("SVG exceeds the 4 MB limit.");
            using var reader = new StreamReader(stream); var text = await reader.ReadToEndAsync();
            var canvas = session.Canvas; var source = canvas.History.Document; var fingerprint = Snapshot(session); var sourceLayer = canvas.ActiveLayer;
            var dialog = new Window { Title = "SVG import options", Width = 660, Height = 370, MinWidth = 540, MinHeight = 320, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background, Foreground = Foreground };
            var tolerance = Number(.001m, 1); tolerance.Value = (decimal)SvgPaths.DefaultCurveTolerance; tolerance.Increment = .01m;
            var info = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap }; bool closed = false; Task? work = null;
            using var lifetime = new CancellationTokenSource();
            Button import = null!;
            async Task Execute()
            {
                import.IsEnabled = tolerance.IsEnabled = false; info.Text = "Reading SVG geometry…"; var selectedTolerance = (double)(tolerance.Value ?? .05m);
                try
                {
                    var parsed = await Task.Run(() => SvgImport.Read(text, 0, selectedTolerance, lifetime.Token), lifetime.Token);
                    if (closed || workspaceClosed || lifetime.IsCancellationRequested) return;
                    if (Current != session || !sessions.Contains(session) || Snapshot(session) != fingerprint || canvas.ActiveLayer != sourceLayer) throw new InvalidDataException("The destination document or layer changed. Close and import again.");
                    var next = source; var target = sourceLayer;
                    if (next.Layers[target].Kind == LayerKind.Image) { next = LayerEditing.Add(next, "Imported SVG", LayerKind.Vector); target = next.Layers.Count - 1; }
                    var shapes = parsed.Select(s => s with { Layer = target }).ToList(); next = next with { Shapes = [.. next.Shapes, .. shapes] }; next.Validate();
                    canvas.CancelDrawing(); canvas.ActiveLayer = target; canvas.Selected = null; foreach (var s in shapes) canvas.Selection.Add(s.Id); canvas.Apply(next);
                    dialog.Close(); status.Text = $"Imported {shapes.Count} SVG objects · curve tolerance {selectedTolerance:0.###} mm · curves are editable polylines. Styles are not imported.";
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) when (ex is InvalidDataException or System.Xml.XmlException or ArgumentException) { if (!closed) info.Text = ex.Message; }
                finally { if (!closed) import.IsEnabled = tolerance.IsEnabled = true; }
            }
            import = Button("Import SVG", () => { if (work?.IsCompleted != false) work = Execute(); });
            var root = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto,*,Auto"), Margin = new Thickness(20) };
            root.Children.Add(Label("Import SVG geometry", 18));
            var field = new StackPanel(); field.Children.Add(Label("Curve tolerance · mm")); tolerance.Width = 125; tolerance.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left; field.Children.Add(tolerance); Grid.SetRow(field, 1); root.Children.Add(field);
            var note = Label("Cubic/quadratic curves become line segments; their original control handles are not retained. Smaller tolerance preserves more detail. Arcs, transforms and CSS styles are not supported yet."); Grid.SetRow(note, 2); root.Children.Add(note);
            Grid.SetRow(info, 3); root.Children.Add(info); var actions = new WrapPanel(); actions.Children.Add(import); actions.Children.Add(Button("Cancel", () => dialog.Close())); Grid.SetRow(actions, 4); root.Children.Add(actions);
            dialog.Content = root; dialog.Closed += (_, _) => { closed = true; lifetime.Cancel(); };
            await dialog.ShowDialog(this); if (work is not null) await work;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or System.Xml.XmlException or UnauthorizedAccessException or NotSupportedException) { status.Text = "Import failed: " + ex.Message; }
    }
}
