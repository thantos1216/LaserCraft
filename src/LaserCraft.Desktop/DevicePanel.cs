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
    private bool querying, disconnecting;
    private DeviceProfile? connectedProfile;
    private ConnectionJournal? connectionJournal;
    private Button? homeButton, runDocumentButton, frameDocumentButton;
    private Button? connectButton, disconnectButton, readStatusButton, readSettingsButton;
    private readonly ComboBox connectionPort = new() { MinWidth = 120, PlaceholderText = "Choose COM port" };
    private Button? refreshConnectionPorts;
    private void RefreshConnectionPorts()
    {
        var prior = connectionPort.SelectedItem as string;
        try
        {
            var ports = TransportFactory.SerialPorts(); connectionPort.ItemsSource = ports;
            var selected=profiles.ElementAtOrDefault(deviceChoice.SelectedIndex);var saved = selected is null?null:preferences.Controls?.GetValueOrDefault(selected.Id)?.Port??selected.SerialPort;
            connectionPort.SelectedItem = ports.FirstOrDefault(p => string.Equals(p, prior ?? saved, StringComparison.OrdinalIgnoreCase));
        }
        catch (IOException ex) { connectionPort.ItemsSource = Array.Empty<string>(); connectionStatus.Text = "Port discovery failed: " + ex.Message; }
    }
    private void UpdateConnectionControls()
    {
        UpdatePlacementControls();
        deviceChoice.IsEnabled = !connecting && !disconnecting && monitor is null;
        if (connectButton is not null) connectButton.IsEnabled = deviceChoice.IsEnabled;
        if (disconnectButton is not null) disconnectButton.IsEnabled = !disconnecting && (connecting || monitor is not null);
        if (readStatusButton is not null) readStatusButton.IsEnabled = monitor is not null && !connecting && !querying && !disconnecting;
        if (readSettingsButton is not null) readSettingsButton.IsEnabled = readStatusButton?.IsEnabled == true;
        connectionPort.IsEnabled = deviceChoice.IsEnabled && profiles.ElementAtOrDefault(deviceChoice.SelectedIndex)?.Connection == ConnectionKind.Serial;
        if (refreshConnectionPorts is not null) refreshConnectionPorts.IsEnabled = connectionPort.IsEnabled;
        if (sessions.Count > 0) RefreshStatus();
        if(homeButton is not null)homeButton.IsEnabled=monitor is not null && connectedProfile?.Controller==ControllerFamily.Grbl && monitor.Simulation is null && !connecting && !querying && !disconnecting;
        if(runDocumentButton is not null)runDocumentButton.IsEnabled=homeButton?.IsEnabled==true;
        if(frameDocumentButton is not null)frameDocumentButton.IsEnabled=!querying&&!connecting&&!disconnecting;
        if(circleFrameButton is not null)circleFrameButton.IsEnabled=frameDocumentButton?.IsEnabled==true;
        jobWorkflow.Text = monitor is null ? "Connect a physical GRBL profile to cut shapes or engrave images. Then Home → position → Preview → Frame laser off → Run." : monitor.Simulation is not null ? "Simulator connection: previews/rehearsal only. Select a physical profile for actual output." : !monitor.HomingConfirmed ? "Next: Home in this connection before framing or running the document." : "Next: position inside the bed, Preview, Frame laser off, then Run. Check Start From and Job Origin before reviewing output.";
        UpdateManualControls();
        UpdateConsoleControls();
    }
    private CancellationTokenSource? connectionAttempt;
    private readonly CancellationTokenSource connectionLifetime = new();
    private string ProfilesPath => Path.Combine(StateFolder, "devices.json");
    private Control BuildConnectionPanel()
    {
        try { profiles = ProfileStore.Load(ProfilesPath); } catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or InvalidDataException) { profiles = [DeviceProfile.Simulator()]; connectionStatus.Text = "Saved profiles could not be read: " + ex.Message; }
        profiles=profiles.Where(p=>p.Connection!=ConnectionKind.Simulator).ToList();if(profiles.Count==0){profiles.Add(new DeviceProfile(Guid.NewGuid(),"Attzr 35 W",Connection:ConnectionKind.Serial,SerialPort:"COM1",BedWidth:430,BedHeight:430));ProfileStore.Save(ProfilesPath,profiles);}
        deviceChoice.SelectionChanged += (_, _) => { if(preferencesLoaded)SavePreferences();connectionPort.SelectedItem = null; RefreshConnectionPorts(); if(preferencesLoaded){RestoreDeviceDefaults();ApplyProfileBed();} UpdateConnectionControls(); };
        ReloadDeviceChoices();
        var panel = new StackPanel { Spacing = 4 }; var row = new WrapPanel(); row.Children.Add(deviceChoice); row.Children.Add(Button("Devices…", () => _ = EditDevices())); row.Children.Add(connectButton = AsyncButton("Connect", ConnectDevice)); row.Children.Add(disconnectButton = AsyncButton("Disconnect", DisconnectDevice)); row.Children.Add(readStatusButton = AsyncButton("Read status", ReadDeviceStatus)); row.Children.Add(readSettingsButton = AsyncButton("Read firmware settings…", ReadFirmwareSettings)); panel.Children.Add(row); panel.Children.Add(Label("Connect waits 1 second, then homes if firmware $22=1 (machine moves). Home is also available while connected.")); panel.Children.Add(connectionStatus); UpdateConnectionControls();
        var usb = new WrapPanel(); usb.Children.Add(Label("USB COM for this connection")); usb.Children.Add(connectionPort); usb.Children.Add(refreshConnectionPorts = Button("Refresh COM ports", RefreshConnectionPorts)); panel.Children.Insert(0, usb);
        UpdateConnectionControls();
        row.Children.Add(AsyncButton("Save connection log…", SaveConnectionLog));
        Closed += (_, _) => { foreach (var s in sessions) s.Canvas.ReleaseImages(); };
        return panel;
    }
    private void ReloadDeviceChoices(int index = 0) { deviceChoice.ItemsSource = profiles.Select(p => $"{p.Name} · {p.Connection}").ToArray(); deviceChoice.SelectedIndex = Math.Clamp(index, 0, profiles.Count - 1); }
    private Task? connectionOperation;
    private Task ConnectDevice()=>connectionOperation=ConnectDeviceCore(true);
    private async Task ConnectDeviceCore(bool automaticHome,bool preserveJournal=false)
    {
        if (connecting || disconnecting || monitor is not null) return; manualConsole.IsChecked = false; consoleCommand.Text = ""; manualTranscript.Text = ""; commandHistory.Clear(); historyIndex = 0; connecting = true; connectionAttempt = CancellationTokenSource.CreateLinkedTokenSource(connectionLifetime.Token); var attempt = connectionAttempt; var profile = profiles[Math.Max(0, deviceChoice.SelectedIndex)]; UpdateConnectionControls();
        connectionStatus.Text = "Connecting to " + profile.Name + "…";
        var journal = connectionJournal = preserveJournal && connectionJournal is not null ? connectionJournal : new ConnectionJournal();
        journal.Add("INFO", "LaserCraft " + typeof(MainWindow).Assembly.GetName().Version + "; connection attempt; profile " + profile.Name);
        try
        {
            if (profile.Connection == ConnectionKind.Serial) profile = profile with { SerialPort = connectionPort.SelectedItem as string ?? throw new InvalidDataException("Select the currently detected USB COM port. Refresh ports after plugging in the laser.") };
            journal.Add("INFO", profile.Connection == ConnectionKind.Serial ? $"USB serial {profile.SerialPort}; baud {profile.BaudRate}; startup wait {profile.SerialStartupDelayMs} ms; DTR {profile.Dtr}; RTS {profile.Rts}" : profile.Connection == ConnectionKind.Tcp ? $"TCP {profile.Host}:{profile.Port}" : "Built-in simulator");
            journal.Add("INFO", FormattableString.Invariant($"Profile bed {profile.BedWidth} × {profile.BedHeight} mm; S maximum {profile.MaxPower}; origin {profile.Origin}. These are profile values, not firmware observations."));
            var transport = await TransportFactory.OpenAsync(profile, attempt.Token); monitor = await GrblMonitor.ConnectAsync(transport, attempt.Token, journal); attempt.Token.ThrowIfCancellationRequested();
            connectedProfile = profile;
            var physical = profile.Connection != ConnectionKind.Simulator && profile.Controller == ControllerFamily.Grbl;
            if (sessions.Count > 0) { StopSimulation(); var canvas=Current.Canvas; var d=canvas.History.Document; if(d.Width!=profile.BedWidth||d.Height!=profile.BedHeight)canvas.Apply(d with {Width=profile.BedWidth,Height=profile.BedHeight});canvas.Fit(); }
            connectionStatus.Text = $"{profile.Name} · {monitor.Identity} · connected"; Log("Connected: " + profile.Name);
            if(physical && automaticHome) { Log("Connection settled: waiting 1 second before checking automatic homing ($22).");connectionStatus.Text="Connected · startup pause / checking homing capability…";var result=await monitor.HomeAfterConnectAsync(profile,attempt.Token);Log(result.Summary); }
            await ReadDeviceStatus();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or NotSupportedException or OperationCanceledException or System.Net.Sockets.SocketException) { journal.Add("ERROR", "Connection: " + ex.GetType().Name + ": " + ex.Message); connectionStatus.Text = attempt.IsCancellationRequested ? "Connection cancelled." : "Connection failed: " + ex.Message + " Check endpoint/baud and close other software using this port. Save connection log for details."; if (monitor is not null) { var previous = monitor; monitor = null; await previous.DisposeAsync(); } connectedProfile = null; }
        finally { connecting = false; connectionAttempt = null; attempt.Dispose(); UpdateConnectionControls(); }
    }
    private async Task ReadDeviceStatus()
    {
        var client = monitor; if (client is null || querying) return; querying = true; UpdateConnectionControls();
        try { var value = await client.QueryStatusAsync(connectionLifetime.Token); if (monitor != client) return; string N(double? n) => n?.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) ?? "not reported"; connectionStatus.Text = $"{connectedProfile?.Name} · {client.Identity} · {value.State}\n{value.Coordinates}: {N(value.X)}, {N(value.Y)}, {N(value.Z)} · reported controller units\nFeed {N(value.Feed)} · spindle/S {N(value.Spindle)} · buffers {value.PlannerAvailable?.ToString() ?? "?"}/{value.ReceiveAvailable?.ToString() ?? "?"} · pins {value.Pins ?? "not reported"} · accessories {value.Accessories ?? "not reported"}\nQuery snapshot · advanced console commands require opt-in; physical document jobs available through Run"; Log(value.Raw); }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidDataException) { if (monitor == client) { connectionJournal?.Add("ERROR", "Status: " + ex.Message); await DisconnectDevice(); connectionStatus.Text = "Status read failed: " + ex.Message; } }
        finally { querying = false; UpdateConnectionControls(); }
    }
    private Task? disconnectOperation;
    private Task DisconnectDevice()
    {
        if(disconnectOperation is {IsCompleted:false})return disconnectOperation;
        return disconnectOperation=DisconnectDeviceCore();
    }
    private async Task DisconnectDeviceCore()
    {
        if (disconnecting) return;var releasingSerial=monitor is not null && connectedProfile?.Connection==ConnectionKind.Serial; manualConsole.IsChecked = false; disconnecting = true; UpdateConnectionControls();
        try { if(monitor?.JobActive==true){try{await monitor.StopJobAsync();}catch(Exception ex)when(ex is IOException or OperationCanceledException or ObjectDisposedException){Log("Abort on disconnect: "+ex.Message);}} if (connectionAttempt is not null) await connectionAttempt.CancelAsync(); if (monitor is not null) { var previous = monitor; monitor = null; await previous.DisposeAsync(); } connectedProfile = null; connectionStatus.Text = "Device disconnected"; }
        finally { if(releasingSerial)await Task.Delay(1000);disconnecting = false; UpdateConnectionControls(); }
    }
    private async Task ReadFirmwareSettings()
    {
        var client = monitor; var profile = connectedProfile; if (client is null || profile is null || querying) return;
        querying = true; UpdateConnectionControls();
        try
        {
            var settings = await client.QuerySettingsAsync(connectionLifetime.Token); if (monitor != client || workspaceClosed) return;
            var maximum = settings.FirstOrDefault(s => s.Number == 30)?.Value; var laser = settings.FirstOrDefault(s => s.Number == 32)?.Value;
            var summary = $"{profile.Name} · {client.Identity}\nProfile S maximum: {profile.MaxPower} · reported $30: {maximum ?? "not reported"} · laser mode $32: {laser ?? "not reported"}";
            if (maximum is not null && double.TryParse(maximum, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var max) && max != profile.MaxPower) summary += "\nProfile S maximum differs from the controller. Review the device profile before offline export.";
            var dialog = new Window { Title = "Firmware settings · read only", Width = 760, Height = 620, MinWidth = 500, MinHeight = 380, WindowStartupLocation = WindowStartupLocation.CenterOwner };
            var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(16) }; root.Children.Add(Label(summary));
            var report = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.NoWrap, FontFamily = Avalonia.Media.FontFamily.Parse("Consolas"), Text = string.Join(Environment.NewLine, settings.Select(s => s.Raw + "    " + s.Description)) }; Grid.SetRow(report, 1); root.Children.Add(report);
            var footer = new StackPanel(); footer.Children.Add(Label($"{settings.Count} reported settings. Only $$ was queried; controller and profile settings were not changed.")); footer.Children.Add(Button("Close", () => dialog.Close())); Grid.SetRow(footer, 2); root.Children.Add(footer); dialog.Content = root; Log("Read " + settings.Count + " firmware settings (read only)."); await dialog.ShowDialog(this);
        }
        catch (Exception ex) when (ex is IOException or OperationCanceledException or InvalidDataException) { if (monitor == client) { connectionJournal?.Add("ERROR", "Settings: " + ex.Message); await DisconnectDevice(); connectionStatus.Text = "Settings read failed: " + ex.Message; } }
        finally { querying = false; UpdateConnectionControls(); }
    }
    private async Task EditDevices()
    {
        if (connecting || disconnecting || monitor is not null) { status.Text = "Disconnect before editing device profiles."; return; }
        var dialog = new Window { Title = "Device profiles", Width = 650, Height = 810, MinHeight = 650, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var name = new TextBox(); var family = new ComboBox { ItemsSource = Enum.GetValues<ControllerFamily>() }; var connection = new ComboBox { ItemsSource = new[]{ConnectionKind.Serial,ConnectionKind.Tcp} };
        var portName = new TextBox { PlaceholderText = "COM3" }; var ports = new ComboBox { MinWidth = 160 };
        void RefreshPorts() { try { ports.ItemsSource = TransportFactory.SerialPorts(); } catch (IOException) { ports.ItemsSource = Array.Empty<string>(); } }
        RefreshPorts();
        ports.SelectionChanged += (_, _) => { if (ports.SelectedItem is string p) portName.Text = p; };
        var baud = new ComboBox { ItemsSource = new[] { 9600, 19200, 38400, 57600, 115200, 230400, 250000, 460800, 921600 } }; var startupDelay = Number(0, 10000); var host = new TextBox { PlaceholderText = "192.168.1.50 or hostname" }; var tcpPort = Number(1, 65535); var bedW = Number(.1m, 10000); var bedH = Number(.1m, 10000); var origin = new ComboBox { ItemsSource = Enum.GetValues<MachineOrigin>() }; var maxPower = Number(1, 1000000);
        var coordinateRange = new ComboBox { ItemsSource=Enum.GetValues<MachineCoordinateRange>() };
        var airSupport = new CheckBox { Content = "Controller has configured air-assist output" }; var airCommand = new ComboBox { ItemsSource = Enum.GetValues<AirAssistOutput>() }; var dtr = new CheckBox { Content = "DTR enabled (may reset controller)" }; var rts = new CheckBox { Content = "RTS enabled" }; var support = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap }; var message = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var list = new ComboBox { ItemsSource = profiles.Select(p => p.Name).ToArray(), SelectedIndex = Math.Max(0, deviceChoice.SelectedIndex) }; var editId = Guid.Empty;
        void Load(DeviceProfile p) { editId = p.Id; name.Text = p.Name; family.SelectedItem = p.Controller; connection.SelectedItem = p.Connection; portName.Text = p.SerialPort; baud.SelectedItem = p.BaudRate; startupDelay.Value = p.SerialStartupDelayMs; host.Text = p.Host; tcpPort.Value = p.Port; bedW.Value = (decimal)p.BedWidth; bedH.Value = (decimal)p.BedHeight; origin.SelectedItem = p.Origin; coordinateRange.SelectedItem=p.CoordinateRange; maxPower.Value = p.MaxPower; airSupport.IsChecked = p.AirAssistEnabled; airCommand.SelectedItem = p.AirOutput; dtr.IsChecked = p.Dtr; rts.IsChecked = p.Rts; }
        family.SelectionChanged += (_, _) => { if (family.SelectedItem is ControllerFamily f) support.Text = ControllerCatalog.Support(f); };
        list.SelectionChanged += (_, _) => { if (list.SelectedIndex >= 0 && list.SelectedIndex < profiles.Count) Load(profiles[list.SelectedIndex]); };
        var panel = new StackPanel { Spacing = 4, Margin = new Thickness(18) }; panel.Children.Add(Label("Device profiles · configuration and query connection", 17)); panel.Children.Add(list); panel.Children.Add(Button("New profile", () => Load(new DeviceProfile(Guid.NewGuid(), "New laser",Connection:ConnectionKind.Serial,SerialPort:"COM1"))));
        panel.Children.Add(Button("Attzr 35 W USB draft · 430 × 430", () =>
        {
            Load(new DeviceProfile(Guid.NewGuid(), "Attzr 35 W · USB (verify settings)", Connection: ConnectionKind.Serial, BedWidth: 430, BedHeight: 430));
            message.Text = "User-specified bed: 430 × 430 mm. Select the detected COM port. 115200 baud, S1000 and bottom-left origin are unverified starting values, not confirmed Attzr defaults. Save does not connect. Run Connection check after connecting; physical control is available after connecting.";
        }));
        Field("Name", name); Field("Controller family", family); panel.Children.Add(support); Field("Connection", connection); Field("Detected serial ports (no port is opened)", ports); panel.Children.Add(Button("Refresh serial ports", RefreshPorts)); Field("Serial port", portName); Field("Baud · 8 data bits, no parity, 1 stop bit", baud); Field("Serial startup delay · ms before identity query", startupDelay); Field("TCP host", host); Field("TCP port", tcpPort);
        var dims = new WrapPanel(); Inline(dims, "Bed width mm", bedW, 120); Inline(dims, "Bed height mm", bedH, 120); Inline(dims, "S-value maximum", maxPower, 120); panel.Children.Add(dims); Field("Origin for output orientation", origin); Field("Machine coordinate range · FromHoming infers each axis sign from the homing report",coordinateRange); panel.Children.Add(airSupport); Field("Configured air-assist output for export/jobs", airCommand); panel.Children.Add(dtr); panel.Children.Add(rts);
        panel.Children.Add(Label("Connect opens only the chosen endpoint and queries firmware identity/status. Opening a serial port can reset some controllers. Connect + Home requests homing when the visible option is checked; uncheck it for query-only connection. The separately enabled manual console can send state-changing commands; reviewed document jobs are available for GRBL. Other controller families can be saved as profiles while their adapters are developed.")); panel.Children.Add(message);
        var buttons = new WrapPanel(); buttons.Children.Add(Button("Save profile", () =>
        {
            try { var p = new DeviceProfile(editId, name.Text ?? "", (ControllerFamily)(family.SelectedItem ?? ControllerFamily.Grbl), (ConnectionKind)(connection.SelectedItem ?? ConnectionKind.Simulator), portName.Text ?? "", (int)(baud.SelectedItem ?? 115200), host.Text ?? "", (int)(tcpPort.Value ?? 23), (double)(bedW.Value ?? 400), (double)(bedH.Value ?? 400), (MachineOrigin)(origin.SelectedItem ?? MachineOrigin.BottomLeft), (int)(maxPower.Value ?? 1000), airSupport.IsChecked == true, dtr.IsChecked == true, rts.IsChecked == true, (int)(startupDelay.Value ?? 2000), (AirAssistOutput)(airCommand.SelectedItem ?? AirAssistOutput.M8), (MachineCoordinateRange)(coordinateRange.SelectedItem ?? MachineCoordinateRange.FromHoming)); p.Validate(); var next = profiles.Where(x => x.Id != p.Id).Append(p).ToList(); ProfileStore.Save(ProfilesPath, next); profiles = next; ReloadDeviceChoices(profiles.FindIndex(x => x.Id == p.Id)); dialog.Close(); }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException) { message.Text = ex.Message; }
        })); buttons.Children.Add(Button("Close", () => dialog.Close())); panel.Children.Add(buttons); dialog.Content = new ScrollViewer { Content = panel }; Load(profiles[Math.Max(0, list.SelectedIndex)]); await dialog.ShowDialog(this);
        void Field(string label, Control control) { panel.Children.Add(Label(label)); panel.Children.Add(control); }
    }
}
