using Avalonia;
using Avalonia.Controls;
using LaserCraft.Devices;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private async Task ReadConnectionCheck()
    {
        var client = monitor; var profile = connectedProfile; if (client is null || profile is null || querying) return;
        querying = true; UpdateConnectionControls(); var started = DateTimeOffset.UtcNow;
        try
        {
            var settings = await client.QuerySettingsAsync(connectionLifetime.Token);
            var modal = await client.QueryModalStateAsync(connectionLifetime.Token);
            var sample = await client.QueryStatusAsync(connectionLifetime.Token);
            if (monitor != client || workspaceClosed) return;
            var ended = DateTimeOffset.UtcNow; var checks = GrblConnectionCheck.Compare(profile, settings, modal, sample);
            var report = $"{profile.Name}\n{client.Identity}\nEndpoint: {profile.Connection} · {(profile.Connection == Domain.ConnectionKind.Serial ? profile.SerialPort + " · baud " + profile.BaudRate : profile.Connection == Domain.ConnectionKind.Simulator ? "built-in simulator" : profile.Host + ":" + profile.Port)}\nCollected {started:u} to {ended:u}; sequential readings, not an atomic or continuously monitored state.\n\n";
            report += string.Join("\n\n", checks.Select(c => $"{c.Finding} · {c.Name}\n{c.Detail}"));
            report += "\n\nSource reports\n" + string.Join("\n", settings.Select(s => s.Raw)) + "\n" + modal.Raw + "\n" + sample.Raw;
            var dialog = new Window { Title = "Connection check · read only", Width = 800, Height = 720, MinWidth = 500, MinHeight = 400, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(16) };
            root.Children.Add(Label("Diagnostic comparison only · physical execution remains unavailable. Match does not mean ready to fire or cut."));
            var text = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Text = report }; Grid.SetRow(text, 1); root.Children.Add(text);
            var close = Button("Close", () => dialog.Close()); Grid.SetRow(close, 2); root.Children.Add(close); dialog.Content = root;
            Log("Connection check queried $$, $G and ?; no settings or machine state changed."); await dialog.ShowDialog(this);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidDataException)
        {
            if (monitor == client) { await DisconnectDevice(); connectionStatus.Text = "Connection check failed: " + ex.Message; }
        }
        finally { querying = false; UpdateConnectionControls(); }
    }
}
