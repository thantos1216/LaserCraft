namespace LaserCraft.Domain;

public enum ControllerFamily { Grbl, GrblHal, FluidNc, Marlin, Smoothieware, Ruida, Trocen }
public enum AirAssistOutput { M8, M7 }
public enum ConnectionKind { Simulator, Serial, Tcp }
public enum MachineOrigin { TopLeft, BottomLeft, TopRight, BottomRight }
public sealed record DeviceProfile(Guid Id, string Name, ControllerFamily Controller = ControllerFamily.Grbl, ConnectionKind Connection = ConnectionKind.Simulator, string SerialPort = "", int BaudRate = 115200, string Host = "", int Port = 23, double BedWidth = 400, double BedHeight = 400, MachineOrigin Origin = MachineOrigin.BottomLeft, int MaxPower = 1000, bool AirAssistEnabled = false, bool Dtr = false, bool Rts = false, int SerialStartupDelayMs = 2000, AirAssistOutput AirOutput = AirAssistOutput.M8)
{
    public static DeviceProfile Simulator() => new(Guid.NewGuid(), "GRBL simulator");
    public void Validate()
    {
        if (Id == Guid.Empty || string.IsNullOrWhiteSpace(Name) || Name.Length > 128 || !Enum.IsDefined(Controller) || !Enum.IsDefined(Connection) || !Enum.IsDefined(Origin)) throw new InvalidDataException("Invalid device profile.");
        if (!double.IsFinite(BedWidth) || !double.IsFinite(BedHeight) || BedWidth <= 0 || BedHeight <= 0 || BedWidth > 10000 || BedHeight > 10000 || MaxPower < 1 || MaxPower > 1000000) throw new InvalidDataException("Invalid machine dimensions or power range.");
        if (!Enum.IsDefined(AirOutput)) throw new InvalidDataException("Invalid air-assist output command.");
        if (SerialStartupDelayMs < 0 || SerialStartupDelayMs > 10000) throw new InvalidDataException("Serial startup delay must be between 0 and 10000 ms.");
        if (BaudRate is not (9600 or 19200 or 38400 or 57600 or 115200 or 230400 or 250000 or 460800 or 921600)) throw new InvalidDataException("Unsupported baud rate.");
        if (Connection == ConnectionKind.Serial && !System.Text.RegularExpressions.Regex.IsMatch(SerialPort ?? "", "^COM[1-9][0-9]{0,3}$", System.Text.RegularExpressions.RegexOptions.IgnoreCase)) throw new InvalidDataException("Choose a Windows COM port.");
        if (Connection == ConnectionKind.Tcp && (string.IsNullOrWhiteSpace(Host) || Uri.CheckHostName(Host) == UriHostNameType.Unknown || Port < 1 || Port > 65535)) throw new InvalidDataException("Enter a TCP host and port (1..65535).");
    }
}
public static class ControllerCatalog
{
    public static bool HasConnectionAdapter(ControllerFamily family) => family is ControllerFamily.Grbl or ControllerFamily.GrblHal or ControllerFamily.FluidNc;
    public static string Support(ControllerFamily family) => family switch
    {
        ControllerFamily.Grbl => "GRBL 1.1 · queries and opt-in manual console; hardware unverified",
        ControllerFamily.GrblHal => "grblHAL · experimental GRBL-compatible queries/console",
        ControllerFamily.FluidNc => "FluidNC · experimental GRBL-compatible queries/console",
        _ => family + " · profile only; connection adapter planned"
    };
}
