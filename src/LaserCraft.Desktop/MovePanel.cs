using Avalonia.Controls;
using Avalonia.Threading;
using LaserCraft.Devices;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private readonly NumericUpDown jogDistance = Number(.001m, 1000), jogFeed = Number(.001m, 100000), pulsePower = Number(.001m, 5), pulseDuration = Number(1, 500);
    private readonly TextBlock manualStatus = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBox manualTranscript = new() { IsReadOnly = true, AcceptsReturn = true, Height = 100, FontFamily = Avalonia.Media.FontFamily.Parse("Consolas") };
    private readonly List<Button> jogButtons = [];
    private Button? pulseButton, stopPulseButton;
    private readonly DispatcherTimer manualTimer = new() { Interval = TimeSpan.FromMilliseconds(25) };
    private readonly System.Diagnostics.Stopwatch manualClock = new();
    private TimeSpan lastManualTime;
    private Control BuildMovePanel()
    {
        jogDistance.Value = 1; jogDistance.Increment = .1m; jogFeed.Value = 600; pulsePower.Value = 1; pulseDuration.Value = 100;
        var panel = new StackPanel { Spacing = 4, Margin = new Avalonia.Thickness(10) };
        panel.Children.Add(Label("Manual control rehearsal · SIMULATOR ONLY", 16));
        panel.Children.Add(Label("Connect a Simulator profile in Laser below. These controls never send motion or firing commands to serial/TCP devices. Simulated +Y is up; coordinates are millimetres within the simulator bed."));
        var jogOptions = new WrapPanel(); Inline(jogOptions, "Distance · mm", jogDistance, 120); Inline(jogOptions, "Feed · mm/min", jogFeed, 120); panel.Children.Add(jogOptions);
        var pad = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto,Auto"), ColumnDefinitions = new ColumnDefinitions("Auto,Auto,Auto"), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        void JogButton(string label, int x, int y, int row, int column)
        {
            var button = Button(label, () => ManualAction(sim => sim.Jog(x * (double)(jogDistance.Value ?? 1), y * (double)(jogDistance.Value ?? 1), (double)(jogFeed.Value ?? 600))));
            Grid.SetRow(button, row); Grid.SetColumn(button, column); pad.Children.Add(button); jogButtons.Add(button);
        }
        JogButton("Y+ ↑", 0, 1, 0, 1); JogButton("← X−", -1, 0, 1, 0); JogButton("X+ →", 1, 0, 1, 2); JogButton("Y− ↓", 0, -1, 2, 1); panel.Children.Add(pad);
        var pulseOptions = new WrapPanel(); Inline(pulseOptions, "Pulse power · %", pulsePower, 120); Inline(pulseOptions, "Duration · ms", pulseDuration, 120); panel.Children.Add(pulseOptions);
        var actions = new WrapPanel();
        pulseButton = Button("Simulate pulse", () => ManualAction(sim => { sim.StartPulse((double)(pulsePower.Value ?? 1), (int)(pulseDuration.Value ?? 100)); manualClock.Restart(); lastManualTime = TimeSpan.Zero; manualTimer.Start(); }));
        stopPulseButton = Button("Stop simulated pulse", () => ManualAction(sim => { sim.Stop(); manualTimer.Stop(); })); actions.Children.Add(pulseButton); actions.Children.Add(stopPulseButton); panel.Children.Add(actions);
        panel.Children.Add(manualStatus); panel.Children.Add(Label("Rehearsal command transcript · no hardware writes")); panel.Children.Add(manualTranscript);
        panel.Children.Add(Label("This is not a live laser test. Physical jogging, firing and job streaming remain unavailable. Rehearsal pulses are limited to 5% / 500 ms; they do not model burn depth or controller timing."));
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
            var sim = monitor?.Simulation ?? throw new InvalidDataException("Connect a Simulator profile to rehearse controls. Physical devices remain read-only.");
            action(sim); UpdateManualControls();
        }
        catch (InvalidDataException ex) { status.Text = ex.Message; }
    }
    private void UpdateManualControls()
    {
        var sim = monitor?.Simulation;
        foreach (var button in jogButtons) button.IsEnabled = sim is not null && !sim.IsPulsing;
        if (pulseButton is not null) pulseButton.IsEnabled = sim is not null && !sim.IsPulsing;
        if (stopPulseButton is not null) stopPulseButton.IsEnabled = sim is not null;
        if (sim is null) { manualTimer.Stop(); manualStatus.Text = "No manual simulator connected. Serial/TCP connections are read-only."; manualTranscript.Text = ""; return; }
        manualStatus.Text = FormattableString.Invariant($"SIMULATED X {sim.Position.X:0.###} · Y {sim.Position.Y:0.###} · S {sim.SValue} · remaining {sim.RemainingPulseSeconds * 1000:0} ms");
        manualTranscript.Text = string.Join(Environment.NewLine, sim.Commands.TakeLast(12));
    }
}
