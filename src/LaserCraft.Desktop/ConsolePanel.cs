using Avalonia.Controls;
using Avalonia.Input;
using LaserCraft.Devices;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private readonly TextBox consoleCommand = new() { PlaceholderText = "One GRBL command, e.g. $$ or $32=1", MaxLength = 256 };
    private readonly CheckBox manualConsole = new() { Content = "Enable setting writes / custom G-code for this connection" };
    private readonly NumericUpDown consoleTimeout = Number(1, 120);
    private readonly TextBlock consoleMode = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private Button? consoleSubmit;
    private readonly List<string> commandHistory = [];
    private int historyIndex;
    private Control BuildConsolePanel()
    {
        consoleTimeout.Value = 10;
        var root = new Grid { RowDefinitions = new RowDefinitions("*,Auto,Auto,Auto"), Margin = new Avalonia.Thickness(6) }; root.Children.Add(console);
        var entry = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") }; entry.Children.Add(consoleCommand); consoleSubmit = AsyncButton("Submit", SubmitConsole); Grid.SetColumn(consoleSubmit, 1); entry.Children.Add(consoleSubmit); Grid.SetRow(entry, 1); root.Children.Add(entry);
        var options = new WrapPanel(); options.Children.Add(manualConsole); Inline(options, "Timeout · seconds", consoleTimeout, 105); options.Children.Add(Button("Clear log", () => console.Text = "")); Grid.SetRow(options, 2); root.Children.Add(options);
        Grid.SetRow(consoleMode, 3); root.Children.Add(consoleMode);
        consoleCommand.TextChanged += (_, _) => UpdateConsoleControls(); manualConsole.IsCheckedChanged += (_, _) => { UpdateConsoleControls(); if (sessions.Count > 0) RefreshStatus(); };
        consoleCommand.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.Enter) { e.Handled = true; await SubmitConsole(); }
            else if (e.Key is Key.Up or Key.Down && commandHistory.Count > 0)
            { e.Handled = true; historyIndex = Math.Clamp(historyIndex + (e.Key == Key.Up ? -1 : 1), 0, commandHistory.Count); consoleCommand.Text = historyIndex == commandHistory.Count ? "" : commandHistory[historyIndex]; consoleCommand.CaretIndex = consoleCommand.Text.Length; }
        };
        UpdateConsoleControls(); return root;
    }
    private void UpdateConsoleControls()
    {
        var available = monitor is not null && !connecting && !querying && !disconnecting;
        consoleCommand.IsEnabled = available; manualConsole.IsEnabled = available; consoleTimeout.IsEnabled = available;
        if (consoleSubmit is not null) consoleSubmit.IsEnabled = available && !string.IsNullOrWhiteSpace(consoleCommand.Text);
        consoleMode.Text = manualConsole.IsChecked == true
            ? "MANUAL COMMANDS may move/fire or change persistent settings. ok means acknowledged, not completed motion. Console/disconnect are not emergency stops. No firmware-image flashing."
            : "Queries: ?, $, $$, $I, $G, $# and $N. Enable manual commands for setting changes or G-code. Enter/Submit sends; Up/Down recalls this session's commands.";
    }
    private async Task SubmitConsole()
    {
        var client = monitor; if (client is null || connecting || querying || disconnecting) return;
        GrblConsoleCommand command;
        try { command = GrblConsoleCommand.Prepare(consoleCommand.Text ?? "", manualConsole.IsChecked == true); }
        catch (InvalidDataException ex) { Log("Console: " + ex.Message); return; }
        var permission = manualConsole.IsChecked == true; querying = true; UpdateConnectionControls();
        if (commandHistory.Count == 100) commandHistory.RemoveAt(0); commandHistory.Add(command.Text); historyIndex = commandHistory.Count; Log("TX attempted > " + command.Text);
        try
        {
            var result = await client.SubmitConsoleAsync(command.Text, permission, (int)(consoleTimeout.Value ?? 10), connectionLifetime.Token);
            foreach (var line in result.Lines) Log("RX < " + line);
            Log("Console result: " + result.Outcome + (result.Outcome == GrblConsoleOutcome.Acknowledged ? " · not proof of completed motion or verified setting readback." : ""));
            if (result.Outcome == GrblConsoleOutcome.Alarm && monitor == client) await DisconnectDevice();
            if (monitor == client) consoleCommand.Text = "";
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidDataException)
        {
            connectionJournal?.Add("ERROR", "Console: " + ex.Message); Log("Console interrupted: " + ex.Message + " Outcome may be unknown; no retry. Disconnecting does not stop motion. Save connection log.");
            if (monitor == client) await DisconnectDevice();
        }
        finally { querying = false; UpdateConnectionControls(); }
    }
}
