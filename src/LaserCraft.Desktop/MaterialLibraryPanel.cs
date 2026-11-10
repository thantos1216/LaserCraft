using Avalonia.Controls;
using LaserCraft.Domain;
using System.Text.Json;
namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private MaterialLibrary materialLibrary = MaterialLibrary.Empty();
    private bool materialLibraryWritable = true;
    private readonly ListBox materialList = new() { Height = 120 };
    private readonly TextBox materialName = new() { PlaceholderText = "Preset name", MaxLength = 128 };
    private readonly TextBox materialNotes = new() { PlaceholderText = "Laser/material notes", MaxLength = 2000, AcceptsReturn = true, Height = 58 };
    private readonly NumericUpDown materialThickness = Number(0, 1000);
    private readonly TextBlock materialMessage = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private string MaterialLibraryPath => Path.Combine(StateFolder, "materials.json");
    private Control BuildMaterialLibrary()
    {
        var panel = new StackPanel { Spacing = 4, Margin = new Avalonia.Thickness(10) };
        panel.Children.Add(Label("Material presets", 16)); panel.Children.Add(Label("Save the active layer's applied settings. Apply to the active Cuts / Layers row; artwork and layer identity stay unchanged. Presets are user recipes, not validated manufacturer settings."));
        panel.Children.Add(materialList); panel.Children.Add(materialName); var row = new WrapPanel(); Inline(row, "Thickness mm · 0 = unspecified", materialThickness, 190); panel.Children.Add(row); panel.Children.Add(materialNotes);
        var actions = new WrapPanel(); actions.Children.Add(Button("Save layer as new", () => SaveMaterial(false))); actions.Children.Add(Button("Replace selected", () => SaveMaterial(true))); actions.Children.Add(Button("Apply to active layer", ApplyMaterial)); actions.Children.Add(AsyncButton("Delete selected", DeleteMaterial)); panel.Children.Add(actions); panel.Children.Add(materialMessage);
        materialList.SelectionChanged += (_, _) =>
        {
            if (materialList.SelectedItem is ListBoxItem { Tag: MaterialPreset p }) { materialName.Text = p.Name; materialNotes.Text = p.Notes; materialThickness.Value = (decimal)(p.ThicknessMm ?? 0); materialMessage.Text = DescribeMaterial(p); }
        };
        try { materialLibrary = MaterialLibraryFile.Load(MaterialLibraryPath); RefreshMaterials(); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        { materialLibraryWritable = false; materialMessage.Text = "Library load failed; saving is disabled to preserve the original file. " + ex.Message; }
        if (!materialLibraryWritable) foreach (var action in actions.Children.OfType<Button>()) action.IsEnabled = false;
        return new ScrollViewer { Content = panel };
    }
    private static string DescribeMaterial(MaterialPreset p) => FormattableString.Invariant($"{p.Settings.Kind} / {p.Settings.Mode} · {p.Settings.Speed:0.###} mm/min · {p.Settings.Power:0.###}% · {p.Settings.Passes} passes · air {(p.Settings.AirAssist ? "on" : "off")} · interval {p.Settings.Interval:0.###} mm. Review suitability for your laser before output.");
    private void RefreshMaterials(Guid? selected = null)
    {
        materialList.Items.Clear();
        foreach (var preset in materialLibrary.Presets.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase))
        { var item = new ListBoxItem { Content = preset.Name + " · " + preset.Settings.Kind, Tag = preset }; materialList.Items.Add(item); if (preset.Id == selected) materialList.SelectedItem = item; }
        if (materialLibrary.Presets.Count == 0) materialMessage.Text = "No presets yet. Apply your layer edits in Cuts / Layers first, then save them here.";
    }
    private void SaveMaterial(bool replace)
    {
        if (!materialLibraryWritable || sessions.Count == 0) return;
        try
        {
            var old = (materialList.SelectedItem as ListBoxItem)?.Tag as MaterialPreset;
            if (replace && old is null) throw new InvalidDataException("Select a preset to replace first.");
            var d = Current.Canvas.History.Document; var thickness = (double)(materialThickness.Value ?? 0);
            var preset = new MaterialPreset(replace ? old!.Id : Guid.NewGuid(), materialName.Text ?? "", materialNotes.Text ?? "", thickness == 0 ? null : thickness, LaserLayerSettings.Capture(d.Layers[Current.Canvas.ActiveLayer]));
            preset.Validate(); var next = materialLibrary with { Presets = replace ? materialLibrary.Presets.Select(p => p.Id == old!.Id ? preset : p).ToList() : [.. materialLibrary.Presets, preset] };
            var hadPrevious = File.Exists(MaterialLibraryPath); MaterialLibraryFile.Save(MaterialLibraryPath, next); materialLibrary = next; RefreshMaterials(preset.Id); materialMessage.Text = "Saved locally. " + (hadPrevious ? "Previous library retained as materials.json.bak. " : "") + DescribeMaterial(preset);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { materialMessage.Text = "Preset not saved: " + ex.Message; }
    }
    private async Task DeleteMaterial()
    {
        if (!materialLibraryWritable || materialList.SelectedItem is not ListBoxItem { Tag: MaterialPreset preset }) { materialMessage.Text = "Select a writable preset first."; return; }
        var dialog = new Window { Title = "Delete material preset", Width = 430, Height = 210, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 8 }; panel.Children.Add(Label("Delete preset " + preset.Name + "? Existing project layers are unaffected. The previous library is kept in materials.json.bak."));
        var actions = new WrapPanel(); actions.Children.Add(Button("Delete preset", () => dialog.Close(true))); actions.Children.Add(Button("Cancel", () => dialog.Close(false))); panel.Children.Add(actions); dialog.Content = panel;
        if (!await dialog.ShowDialog<bool>(this)) return;
        try
        {
            var next = materialLibrary with { Presets = materialLibrary.Presets.Where(p => p.Id != preset.Id).ToList() };
            MaterialLibraryFile.Save(MaterialLibraryPath, next); materialLibrary = next; RefreshMaterials(); materialMessage.Text = "Preset deleted; project layers unchanged. Previous library retained as .bak.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException) { materialMessage.Text = "Preset not deleted: " + ex.Message; }
    }
    private void ApplyMaterial()
    {
        if (sessions.Count == 0) return;
        try
        {
            var preset = (materialList.SelectedItem as ListBoxItem)?.Tag as MaterialPreset ?? throw new InvalidDataException("Select a preset first.");
            var canvas = Current.Canvas; var layer = canvas.ActiveLayer; var next = preset.Apply(canvas.History.Document, layer); canvas.CancelDrawing(); canvas.Apply(next);
            materialMessage.Text = "Applied to layer " + layer + " · one Undo step. No machine commands sent. " + DescribeMaterial(preset);
        }
        catch (InvalidDataException ex) { materialMessage.Text = ex.Message; }
    }
}
