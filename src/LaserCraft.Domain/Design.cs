using System.Text.Json;
using System.Xml.Linq;
using System.Globalization;

namespace LaserCraft.Domain;

public enum ShapeKind { Rectangle, Ellipse }
public sealed record DesignShape(Guid Id, ShapeKind Kind, double X, double Y, double Width, double Height, int Layer = 0);
public sealed record CutLayer(string Name, string Color, double Speed = 1200, double Power = 20, bool Output = true);
public sealed record DesignDocument(int Version, string Name, double Width, double Height, List<DesignShape> Shapes, List<CutLayer> Layers)
{
    public static DesignDocument Empty(string name = "Untitled") => new(1, name, 400, 400, [], [new("Outline", "#47D7F0"), new("Detail", "#B99CFF")]);
    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("Unsupported project version.");
        if (!FinitePositive(Width) || !FinitePositive(Height) || Width > 10000 || Height > 10000) throw new InvalidDataException("Invalid bed size.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length>256 || Shapes is null || Layers is null || Layers.Count == 0 || Layers.Count>256 || Shapes.Count > 100000 || Shapes.Any(s=>s is null) || Layers.Any(l=>l is null)) throw new InvalidDataException("Invalid project contents.");
        if (Shapes.Select(s => s.Id).Distinct().Count() != Shapes.Count) throw new InvalidDataException("Duplicate object IDs.");
        foreach (var s in Shapes)
            if (!Enum.IsDefined(s.Kind) || !double.IsFinite(s.X) || !double.IsFinite(s.Y) || Math.Abs(s.X)>10000 || Math.Abs(s.Y)>10000 || !FinitePositive(s.Width) || !FinitePositive(s.Height) || s.Width>10000 || s.Height>10000 || s.Layer < 0 || s.Layer >= Layers.Count)
                throw new InvalidDataException("Invalid shape geometry or layer.");
        foreach (var l in Layers)
            if (string.IsNullOrWhiteSpace(l.Name) || !System.Text.RegularExpressions.Regex.IsMatch(l.Color??"", "^#[0-9a-fA-F]{6}$") || !FinitePositive(l.Speed) || l.Speed>100000 || !double.IsFinite(l.Power) || l.Power < 0 || l.Power > 100) throw new InvalidDataException("Invalid cut settings.");
    }
    private static bool FinitePositive(double v) => double.IsFinite(v) && v > 0;
}

public sealed class EditorHistory
{
    private readonly Stack<DesignDocument> undo = new(), redo = new();
    public DesignDocument Document { get; private set; } = DesignDocument.Empty();
    private static DesignDocument Copy(DesignDocument d) => d with { Shapes = [.. d.Shapes], Layers = [.. d.Layers] };
    public void Apply(DesignDocument next) { next.Validate(); undo.Push(Copy(Document)); Document = Copy(next); redo.Clear(); }
    public void Reset(DesignDocument next) { next.Validate(); Document = Copy(next); undo.Clear(); redo.Clear(); }
    public void Undo() { if (undo.TryPop(out var d)) { redo.Push(Copy(Document)); Document = d; } }
    public void Redo() { if (redo.TryPop(out var d)) { undo.Push(Copy(Document)); Document = d; } }
}

public static class ProjectFile
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static void Save(string path, DesignDocument document)
    {
        document.Validate();
        var temporary = Path.GetFullPath(path) + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, document, Options); stream.Flush(true); }
            File.Move(temporary, path, true);
        } finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static DesignDocument Load(string path)
    {
        if (new FileInfo(path).Length > 32 * 1024 * 1024) throw new InvalidDataException("Project exceeds the 32 MB limit.");
        var document = JsonSerializer.Deserialize<DesignDocument>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty project.");
        document.Validate(); return document;
    }
}

public static class SvgExport
{
    public static string Write(DesignDocument d)
    {
        d.Validate(); XNamespace ns = "http://www.w3.org/2000/svg";
        static string N(double n) => n.ToString("0.####", CultureInfo.InvariantCulture);
        var root = new XElement(ns + "svg", new XAttribute("width", N(d.Width) + "mm"), new XAttribute("height", N(d.Height) + "mm"), new XAttribute("viewBox", $"0 0 {N(d.Width)} {N(d.Height)}"));
        foreach (var s in d.Shapes)
        {
            var e = s.Kind == ShapeKind.Rectangle
                ? new XElement(ns + "rect", new XAttribute("x", N(s.X)), new XAttribute("y", N(s.Y)), new XAttribute("width", N(s.Width)), new XAttribute("height", N(s.Height)))
                : new XElement(ns + "ellipse", new XAttribute("cx", N(s.X + s.Width / 2)), new XAttribute("cy", N(s.Y + s.Height / 2)), new XAttribute("rx", N(s.Width / 2)), new XAttribute("ry", N(s.Height / 2)));
            e.Add(new XAttribute("fill", "none"), new XAttribute("stroke", d.Layers[s.Layer].Color), new XAttribute("stroke-width", "0.3")); root.Add(e);
        }
        return root.ToString();
    }
}
