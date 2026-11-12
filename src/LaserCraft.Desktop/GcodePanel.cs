using Avalonia.Platform.Storage;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private async Task SaveGcode()
    {
        if(!CommitLayerEdits())return;
        var session = Current; var fingerprint = Snapshot(session); var scope = CurrentOutputScope;
        var profile = profiles[Math.Max(0, deviceChoice.SelectedIndex)];
        try
        {
            var prepared=await PrepareCurrentOutputAsync(connectionLifetime.Token);
            if (workspaceClosed || Current != session || Snapshot(session) != fingerprint || CurrentOutputScope != scope) return;
            var program = GrblExport.Write(prepared.Plan, profile);
            var file = await StorageProvider.SaveFilePickerAsync(new()
            {
                Title = "Save offline GRBL 1.1 program",
                SuggestedFileName = "design.gcode",
                DefaultExtension = "gcode",
                FileTypeChoices = [new("GRBL G-code") { Patterns = ["*.gcode", "*.nc"] }]
            });
            if (file?.TryGetLocalPath() is not string path) return;
            if (workspaceClosed || Current != session || Snapshot(session) != fingerprint || CurrentOutputScope != scope) { status.Text = "Design changed; export again to save the current job."; return; }
            await File.WriteAllTextAsync(path, program);
            status.Text = "Saved GRBL file using " + profile.Name + ". No program was sent to a device.";
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or OperationCanceledException)
        { status.Text = "G-code export failed: " + ex.Message; }
    }
}
