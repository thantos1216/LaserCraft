using Avalonia.Controls;
using Avalonia.Threading;
using LaserCraft.Devices;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private readonly NumericUpDown jogDistance = Number(.001m, 1000), jogFeed = Number(.001m, 100000), pulsePower = Number(.001m, 5), pulseDuration = Number(1, 500);
    private readonly TextBlock manualStatus = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox manualTranscript = new() { IsReadOnly = true, AcceptsReturn = true, Height = 100, FontFamily = Avalonia.Media.FontFamily.Parse("Consolas") };
    private readonly List<Button> jogButtons = [];
    private Button? pulseButton, stopPulseButton, cancelJogButton;
    private readonly CheckBox physicalControl = new() { Content = "Enable physical GRBL control for this connection" };
    private readonly CheckBox physicalFire = new() { Content = "Enable firing test; area clear, laser protection and physical stop ready" };
    private readonly DispatcherTimer manualTimer = new() { Interval = TimeSpan.FromMilliseconds(25) };
    private readonly System.Diagnostics.Stopwatch manualClock = new();
    private TimeSpan lastManualTime;
    private Control BuildMovePanel()
    {
        jogDistance.Value = 1; jogDistance.Increment = .1m; jogFeed.Value = 600; pulsePower.Value = 1; pulseDuration.Value = 100;
        var panel = new StackPanel { Spacing = 4, Margin = new Avalonia.Thickness(10) };
        panel.Children.Add(Label("Manual control · simulator or GRBL 1.1", 16));
        panel.Children.Add(Label("Connect below. Simulator rehearses only. For physical GRBL 1.1, enable control explicitly. Jog is relative in millimetres; axis direction follows firmware, not canvas origin. Start at 1 mm with clearance from all limits. No homing/position bounds are assumed."));
        panel.Children.Add(physicalControl); panel.Children.Add(physicalFire);
        physicalControl.IsCheckedChanged += (_, _) => { if (physicalControl.IsChecked != true) physicalFire.IsChecked = false; UpdateManualControls(); };
        physicalFire.IsCheckedChanged += (_, _) => UpdateManualControls();
        var jogOptions = new WrapPanel(); Inline(jogOptions, "Distance · mm", jogDistance, 120); Inline(jogOptions, "Feed · mm/min", jogFeed, 120); panel.Children.Add(jogOptions);
        var pad = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        void JogButton(string label, int x, int y, int row, int column)
        {
            var button = AsyncButton(label, async () =>
            {
                if (monitor?.Simulation is not null) ManualAction(sim => sim.Jog(x * (double)(jogDistance.Value ?? 1), y * (double)(jogDistance.Value ?? 1), (double)(jogFeed.Value ?? 600)));
                else await PhysicalAction(new(x != 0 ? GrblManualAction.JogX : GrblManualAction.JogY, (x != 0 ? x : y) * (double)(jogDistance.Value ?? 1), (double)(jogFeed.Value ?? 600)));
            });
            Grid.SetRow(button, row); Grid.SetColumn(button, column); pad.Children.Add(button); jogButtons.Add(button);
        }
        JogButton("Y+ ↑", 0, 1, 0, 1); JogButton("← X−", -1, 0, 1, 0); JogButton("X+ →", 1, 0, 1, 2); JogButton("Y− ↓", 0, -1, 2, 1); panel.Children.Add(pad);
        var pulseOptions = new WrapPanel(); Inline(pulseOptions, "Pulse power · %", pulsePower, 120); Inline(pulseOptions, "Duration · ms", pulseDuration, 120); panel.Children.Add(pulseOptions);
        var actions = new WrapPanel();
        pulseButton = AsyncButton("Pulse test", async () =>
        {
            if (monitor?.Simulation is not null) ManualAction(sim => { sim.StartPulse((double)(pulsePower.Value ?? 1), (int)(pulseDuration.Value ?? 100)); manualClock.Restart(); lastManualTime = TimeSpan.Zero; manualTimer.Start(); });
            else await PhysicalAction(new(GrblManualAction.Pulse, PowerPercent: (double)(pulsePower.Value ?? 1), Milliseconds: (int)(pulseDuration.Value ?? 100)));
        });
        stopPulseButton = AsyncButton("Laser off (M5)", async () => { if (monitor?.Simulation is not null) ManualAction(sim => { sim.Stop(); manualTimer.Stop(); }); else await PhysicalAction(new(GrblManualAction.LaserOff)); });
        cancelJogButton = AsyncButton("Cancel jog", () => PhysicalAction(new(GrblManualAction.CancelJog))); actions.Children.Add(cancelJogButton); actions.Children.Add(pulseButton); actions.Children.Add(stopPulseButton); panel.Children.Add(actions);
        panel.Children.Add(manualStatus); panel.Children.Add(Label("Manual-control result / simulator transcript")); panel.Children.Add(manualTranscript);
        panel.Children.Add(Label("Physical limits: jog 0.001–10 mm at 60–3000 mm/min; pulse up to 5% / 500 ms. Pulse reads $30/$31/$32 and queues G1/G94 F60, M3, dwell and M5. Nominal dwell is not measured optical duration; a partial USB write/controller failure can prevent off. Use the physical stop/power switch if needed. Cancel jog and M5 are not emergency stops and wait for any exchange in progress. Disconnect/closing the app does not stop hardware. Job streaming remains unavailable."));
        manualTimer.Tick += (_, _) =>
        {
            var sim = monitor?.Simulation; if (sim is null) { manualTimer.Stop(); UpdateManualControls(); return; }
            var now = manualClock.Elapsed; sim.Advance(now - lastManualTime); lastManualTime = now; UpdateManualControls(); if (!sim.IsPulsing) manualTimer.Stop();
        };
        Closed += (_, _) => { manualTimer.Stop(); monitor?.Simulation?.Stop(); };
        UpdateManualControls(); return new ScrollViewer { Content = panel };
    }
    private void ManualAction(Action<ManualControlSimulation> action)
    {
        try
        {
            var sim = monitor?.Simulation ?? throw new InvalidDataException("Connect a Simulator profile to rehearse controls.");
            action(sim); UpdateManualControls();
        }
        catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
    private async Task PhysicalAction(GrblManualRequest request)
    {
        var client = monitor; var profile = connectedProfile;
        if (client is null || profile is null || connecting || querying || disconnecting) return;
        var allowMotion = physicalControl.IsChecked == true; var allowFire = physicalFire.IsChecked == true;
        querying = true; UpdateConnectionControls();
        string resultText;
        try
        {
            var result = await client.ExecuteManualAsync(profile, request, allowMotion, allowFire, connectionLifetime.Token);
            foreach (var line in result.Lines) Log("Manual " + line);
            manualTranscript.Text = string.Join(Environment.NewLine, result.Lines.TakeLast(16)); resultText = result.Summary;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or OperationCanceledException)
        {
            resultText = "Manual test: " + ex.Message + " No automatic retry. Check hardware/output; save connection log.";
            Log(resultText); connectionJournal?.Add("ERROR", resultText);
            if (monitor == client && (client.RequiresReconnect || ex is IOException or OperationCanceledException)) await DisconnectDevice();
        }
        finally { querying = false; UpdateConnectionControls(); }
        manualStatus.Text = resultText;
    }
    private void UpdateManualControls()
    {
        var sim = monitor?.Simulation; var busy = connecting || querying || disconnecting;
        var physical = monitor is not null && sim is null && connectedProfile?.Controller == ControllerFamily.Grbl;
        physicalControl.IsEnabled = physical && !busy; physicalFire.IsEnabled = physical && !busy && physicalControl.IsChecked == true;
        var enabled = !busy && (sim is not null && !sim.IsPulsing || physical && physicalControl.IsChecked == true);
        foreach (var button in jogButtons) button.IsEnabled = enabled;
        if (pulseButton is not null) pulseButton.IsEnabled = enabled && (sim is not null || physicalFire.IsChecked == true);
        if (stopPulseButton is not null) stopPulseButton.IsEnabled = !busy && (sim is not null || physical && physicalControl.IsChecked == true);
        if (cancelJogButton is not null) cancelJogButton.IsEnabled = !busy && physical && physicalControl.IsChecked == true;
        if (sim is null) { manualTimer.Stop(); if (!querying) manualStatus.Text = physical ? "Physical GRBL connection · enable control, then one action at a time. Read status after each jog. Hardware unverified." : "Connect a simulator or GRBL 1.1 profile. Controls default to disabled."; return; }
        manualStatus.Text = FormattableString.Invariant($"SIMULATED X {sim.Position.X:0.###} · Y {sim.Position.Y:0.###} · S {sim.SValue} · remaining {sim.RemainingPulseSeconds * 1000:0} ms");
        manualTranscript.Text = string.Join(Environment.NewLine, sim.Commands.TakeLast(12));
    }
}
