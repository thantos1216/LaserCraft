using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using LaserCraft.Domain;
using LaserCraft.Devices;
namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private List<DeviceProfile> profiles = [];
    private readonly ComboBox deviceChoice = new() { MinWidth = 220 };
    private readonly TextBlock connectionStatus = new() { Text = "Device disconnected", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private GrblMonitor? monitor;
    private bool connecting;
    private CancellationTokenSource? connectionAttempt;
    private readonly CancellationTokenSource connectionLifetime = new();
    private string ProfilesPath => Path.Combine(StateFolder, "devices.json");
    private Control BuildConnectionPanel()
    {
        try { profiles = ProfileStore.Load(ProfilesPath); } catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException) { profiles = [DeviceProfile.Simulator()]; connectionStatus.Text = "Saved profiles could not be read: " + ex.Message; }
        ReloadDeviceChoices();
        var panel = new StackPanel { Spacing = 4 }; var row = new WrapPanel(); row.Children.Add(deviceChoice); row.Children.Add(Button("Devices…", () => _ = EditDevices())); row.Children.Add(AsyncButton("Connect", ConnectDevice)); row.Children.Add(AsyncButton("Disconnect", DisconnectDevice)); row.Children.Add(AsyncButton("Read status", ReadDeviceStatus)); row.Children.Add(Button("Apply device bed", () => { var p = profiles[Math.Max(0, deviceChoice.SelectedIndex)]; Current.Canvas.Apply(Current.Canvas.History.Document with { Width = p.BedWidth, Height = p.BedHeight }); Current.Canvas.Fit(); })); panel.Children.Add(row); panel.Children.Add(connectionStatus);
        Closed += async (_, _) => { await connectionLifetime.CancelAsync(); if (monitor is not null) await monitor.DisposeAsync(); foreach (var s in sessions) s.Canvas.ReleaseImages(); };
        return panel;
    }
    private void ReloadDeviceChoices(int index = 0) { deviceChoice.ItemsSource = profiles.Select(p => $"{p.Name} · {p.Connection}").ToArray(); deviceChoice.SelectedIndex = Math.Clamp(index, 0, profiles.Count - 1); }
    private async Task ConnectDevice()
    {
        if (connecting || monitor is not null) return; connecting = true; connectionAttempt = CancellationTokenSource.CreateLinkedTokenSource(connectionLifetime.Token); var attempt = connectionAttempt; var profile = profiles[Math.Max(0, deviceChoice.SelectedIndex)];
        connectionStatus.Text = "Connecting to " + profile.Name + "…";
        try
        {
            var transport = await TransportFactory.OpenAsync(profile, attempt.Token); monitor = await GrblMonitor.ConnectAsync(transport, attempt.Token); attempt.Token.ThrowIfCancellationRequested();
            connectionStatus.Text = $"{profile.Name} · {monitor.Identity} · read-only monitor"; Log("Connected: " + profile.Name + " (identity/status queries only)"); await ReadDeviceStatus();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or OperationCanceledException or System.Net.Sockets.SocketException) { connectionStatus.Text = "Connection failed: " + ex.Message; if (monitor is not null) { await monitor.DisposeAsync(); monitor = null; } }
        finally { connecting = false; connectionAttempt = null; attempt.Dispose(); }
    }
    private async Task ReadDeviceStatus()
    {
        var client = monitor; if (client is null) return;
        try { var value = await client.QueryStatusAsync(connectionLifetime.Token); if (monitor != client) return; connectionStatus.Text = $"{client.Identity} · {value.State} · {value.Coordinates} {value.X:0.###}, {value.Y:0.###}, {value.Z:0.###}"; Log(value.Raw); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidDataException) { if (monitor == client) { await DisconnectDevice(); connectionStatus.Text = "Status read failed: " + ex.Message; } }
    }
    private async Task DisconnectDevice() { if (connectionAttempt is not null) await connectionAttempt.CancelAsync(); if (monitor is not null) { var previous = monitor; monitor = null; await previous.DisposeAsync(); } connectionStatus.Text = "Device disconnected"; }
    private async Task EditDevices()
    {
        var dialog = new Window { Title = "Device profiles", Width = 650, Height = 810, MinHeight = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var name = new TextBox(); var family = new ComboBox { ItemsSource = Enum.GetValues<ControllerFamily>() }; var connection = new ComboBox { ItemsSource = Enum.GetValues<ConnectionKind>() };
        var portName = new TextBox { PlaceholderText = "COM3" }; var ports = new ComboBox { MinWidth = 160 }; try { ports.ItemsSource = TransportFactory.SerialPorts(); } catch (IOException) { ports.ItemsSource = Array.Empty<string>(); }
        ports.SelectionChanged += (_, _) => { if (ports.SelectedItem is string p) portName.Text = p; };
        var baud = new ComboBox { ItemsSource = new[] { 9600, 19200, 38400, 57600, 115200, 230400, 250000, 460800, 921600 } }; var host = new TextBox { PlaceholderText = "192.168.1.50 or hostname" }; var tcpPort = Number(1, 65535); var bedW = Number(.1m, 10000); var bedH = Number(.1m, 10000); var origin = new ComboBox { ItemsSource = Enum.GetValues<MachineOrigin>() }; var maxPower = Number(1, 1000000);
        var airSupport = new CheckBox { Content = "Controller has configured air-assist output" }; var dtr = new CheckBox { Content = "DTR enabled (may reset controller)" }; var rts = new CheckBox { Content = "RTS enabled" }; var support = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap }; var message = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var list = new ComboBox { ItemsSource = profiles.Select(p => p.Name).ToArray(), SelectedIndex = Math.Max(0, deviceChoice.SelectedIndex) }; var editId = Guid.Empty;
        void Load(DeviceProfile p) { editId = p.Id; name.Text = p.Name; family.SelectedItem = p.Controller; connection.SelectedItem = p.Connection; portName.Text = p.SerialPort; baud.SelectedItem = p.BaudRate; host.Text = p.Host; tcpPort.Value = p.Port; bedW.Value = (decimal)p.BedWidth; bedH.Value = (decimal)p.BedHeight; origin.SelectedItem = p.Origin; maxPower.Value = p.MaxPower; airSupport.IsChecked = p.AirAssistEnabled; dtr.IsChecked = p.Dtr; rts.IsChecked = p.Rts; }
        family.SelectionChanged += (_, _) => { if (family.SelectedItem is ControllerFamily f) support.Text = ControllerCatalog.Support(f); };
        list.SelectionChanged += (_, _) => { if (list.SelectedIndex >= 0 && list.SelectedIndex < profiles.Count) Load(profiles[list.SelectedIndex]); };
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(18) }; panel.Children.Add(Label("Device profiles · configuration and read-only connection", 17)); panel.Children.Add(list); panel.Children.Add(Button("New profile", () => Load(new DeviceProfile(Guid.NewGuid(), "New laser"))));
        Field("Name", name); Field("Controller family", family); panel.Children.Add(support); Field("Connection", connection); Field("Detected serial ports (no port is opened)", ports); Field("Serial port", portName); Field("Baud · 8 data bits, no parity, 1 stop bit", baud); Field("TCP host", host); Field("TCP port", tcpPort);
        var dims = new WrapPanel(); Inline(dims, "Bed width mm", bedW, 120); Inline(dims, "Bed height mm", bedH, 120); Inline(dims, "S-value maximum", maxPower, 120); panel.Children.Add(dims); Field("Machine origin (stored for future coordinate mapping)", origin); panel.Children.Add(airSupport); panel.Children.Add(dtr); panel.Children.Add(rts);
        panel.Children.Add(Label("Connect opens only the chosen endpoint and queries firmware identity/status. Opening a serial port can reset some controllers. Homing, motion, firing, job streaming and air commands are not sent. Other controller families can be saved as profiles while their adapters are developed.")); panel.Children.Add(message);
        var buttons = new WrapPanel(); buttons.Children.Add(Button("Save profile", () =>
        {
            try { var p = new DeviceProfile(editId, name.Text ?? "", (ControllerFamily)(family.SelectedItem ?? ControllerFamily.Grbl), (ConnectionKind)(connection.SelectedItem ?? ConnectionKind.Simulator), portName.Text ?? "", (int)(baud.SelectedItem ?? 115200), host.Text ?? "", (int)(tcpPort.Value ?? 23), (double)(bedW.Value ?? 400), (double)(bedH.Value ?? 400), (MachineOrigin)(origin.SelectedItem ?? MachineOrigin.BottomLeft), (int)(maxPower.Value ?? 1000), airSupport.IsChecked == true, dtr.IsChecked == true, rts.IsChecked == true); p.Validate(); var next = profiles.Where(x => x.Id != p.Id).Append(p).ToList(); ProfileStore.Save(ProfilesPath, next); profiles = next; ReloadDeviceChoices(profiles.FindIndex(x => x.Id == p.Id)); dialog.Close(); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { message.Text = ex.Message; }
        })); buttons.Children.Add(Button("Close", () => dialog.Close())); panel.Children.Add(buttons); dialog.Content = new ScrollViewer { Content = panel }; Load(profiles[Math.Max(0, list.SelectedIndex)]); await dialog.ShowDialog(this);
        void Field(string label, Control control) { panel.Children.Add(Label(label)); panel.Children.Add(control); }
    }
}
