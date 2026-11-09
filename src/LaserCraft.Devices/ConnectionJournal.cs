using System.Globalization;
using System.Text;

namespace LaserCraft.Devices;

public sealed record ConnectionJournalEntry(DateTimeOffset Time, string Direction, string Text);
public sealed record ConnectionJournalSnapshot(IReadOnlyList<ConnectionJournalEntry> Entries, int Dropped)
{
    public string Format()
    {
        var text = new StringBuilder("LaserCraft connection log · UTC timestamps\nTX = attempted write, not acknowledgement or completed execution.\n");
        text.AppendLine($"Older entries dropped: {Dropped.ToString(CultureInfo.InvariantCulture)}");
        foreach (var entry in Entries) text.Append(entry.Time.ToString("O", CultureInfo.InvariantCulture)).Append("  ").Append(entry.Direction).Append("  ").AppendLine(Escape(entry.Text));
        return text.ToString();
    }
    private static string Escape(string value)
    {
        var text = new StringBuilder();
        foreach (char c in value) switch (c)
        {
            case '\\': text.Append("\\\\"); break;
            case '\r': text.Append("\\r"); break;
            case '\n': text.Append("\\n"); break;
            case '\t': text.Append("\\t"); break;
            default: if (char.IsControl(c)) text.Append("\\u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture)); else text.Append(c); break;
        }
        return text.ToString();
    }
}
public sealed class ConnectionJournal
{
    public const int Capacity = 512;
    public const int EntryLimit = 4096;
    private readonly object sync = new();
    private readonly Queue<ConnectionJournalEntry> entries = new();
    private int dropped;
    public void Add(string direction, string text)
    {
        if (direction is not ("INFO" or "TX" or "RX" or "ERROR")) throw new ArgumentException("Unknown journal direction.", nameof(direction));
        var bounded = text.Length > EntryLimit ? text[..EntryLimit] + " [truncated]" : text;
        lock (sync) { if (entries.Count == Capacity) { entries.Dequeue(); dropped++; } entries.Enqueue(new(DateTimeOffset.UtcNow, direction, bounded)); }
    }
    public ConnectionJournalSnapshot Snapshot() { lock (sync) return new(Array.AsReadOnly(entries.ToArray()), dropped); }
}

internal sealed class JournaledGrblTransport(IGrblTransport inner, ConnectionJournal journal) : IGrblTransport
{
    public async ValueTask WriteAsync(string command, CancellationToken cancellationToken)
    {
        journal.Add("TX", command);
        try { await inner.WriteAsync(command, cancellationToken); }
        catch (Exception ex) { journal.Add("ERROR", "Write: " + ex.GetType().Name + ": " + ex.Message); throw; }
    }
    public async ValueTask<string> ReadLineAsync(CancellationToken cancellationToken)
    {
        try { var line = await inner.ReadLineAsync(cancellationToken); journal.Add("RX", line); return line; }
        catch (Exception ex) { journal.Add("ERROR", "Read: " + ex.GetType().Name + ": " + ex.Message); throw; }
    }
    public async ValueTask DisposeAsync()
    {
        try { await inner.DisposeAsync(); journal.Add("INFO", "Transport closed."); }
        catch (Exception ex) { journal.Add("ERROR", "Close: " + ex.GetType().Name + ": " + ex.Message); throw; }
    }
}
