using Avalonia.Platform.Storage;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private async Task SaveConnectionLog()
    {
        var snapshot = connectionJournal?.Snapshot();
        if (snapshot is null) { status.Text = "No connection attempt recorded yet. Use Connect first."; return; }
        try
        {
            var file = await StorageProvider.SaveFilePickerAsync(new()
            {
                Title = "Save latest connection attempt log", SuggestedFileName = "LaserCraft-connection-" + DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture) + ".txt",
                DefaultExtension = "txt", FileTypeChoices = [new("Text log") { Patterns = ["*.txt"] }]
            });
            if (file?.TryGetLocalPath() is not string path) return;
            await File.WriteAllTextAsync(path, snapshot.Format()); status.Text = "Connection log saved. Includes selected endpoint/profile and raw controller replies; no upload was performed.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = "Could not save connection log: " + ex.Message; }
    }
}
