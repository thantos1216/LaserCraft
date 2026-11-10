namespace LaserCraft.Devices;

public sealed record GrblConsoleCommand(string Text, string Wire, bool IsQuery)
{
    public static GrblConsoleCommand Prepare(string input, bool allowManual)
    {
        if (input.Any(c => c < ' ' || c > '~')) throw new InvalidDataException("Enter one printable ASCII command; newlines, control bytes and non-ASCII characters are not accepted.");
        var command = input.Trim(' ');
        if (command.Length == 0 || command.Length > 79) throw new InvalidDataException("Enter one command of 1–79 ASCII characters (GRBL line-buffer limit).");
        if (command != "?" && command.Any(c => c is '?' or '!' or '~')) throw new InvalidDataException("Embedded real-time characters are not supported, including in comments. Use ? alone for status. Hold/resume/reset require separate controls.");
        bool query = command is "?" or "$" or "$$" or "$I" or "$G" or "$#" or "$N";
        if (!query && !allowManual) throw new InvalidDataException("Query mode permits ?, $, $$, $I, $G, $# and $N. Enable manual commands for setting writes or custom G-code.");
        return new(command, command == "?" ? command : command + "\n", query);
    }
}
public enum GrblConsoleOutcome { Acknowledged, Rejected, Alarm, Status }
public sealed record GrblConsoleResult(GrblConsoleOutcome Outcome, IReadOnlyList<string> Lines);
