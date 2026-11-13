using System.Text.Json;
using System.Xml.Linq;
using System.Globalization;

namespace LaserCraft.Domain;

public enum ShapeKind { Rectangle, Ellipse, Image, Path }
public sealed record RasterAsset(Guid Id, string MimeType, string DataBase64, int PixelWidth, int PixelHeight)
{
    public void Validate()
    {
        if (Id == Guid.Empty || MimeType is not ("image/png" or "image/jpeg" or "image/bmp") || PixelWidth < 1 || PixelHeight < 1 || PixelWidth > 16000 || PixelHeight > 16000 || (long)PixelWidth * PixelHeight > 40_000_000 || string.IsNullOrEmpty(DataBase64) || DataBase64.Length > 12_000_000 || DataBase64.Length % 4 != 0 || !System.Text.RegularExpressions.Regex.IsMatch(DataBase64, "^[A-Za-z0-9+/]*={0,2}$", System.Text.RegularExpressions.RegexOptions.NonBacktracking))
            throw new InvalidDataException("Invalid or oversized embedded raster image.");
    }
}
public sealed record DesignShape(Guid Id, ShapeKind Kind, double X, double Y, double Width, double Height, int Layer = 0, RasterAsset? Image = null, bool FlipX = false, bool FlipY = false, PathData? Path = null, TextSource? Text = null, Guid? GroupId = null);
public enum CutMode { Line, Fill, Image }
public enum PassOrderMode { WholeLayer, PerObject }
public enum LayerKind { Vector, Image }
public sealed record CutLayer(string Name, string Color, double Speed = 1200, double Power = 20, bool Output = true, CutMode Mode = CutMode.Line, int Passes = 1, bool Visible = true, bool AirAssist = false, double Interval = .1, LayerKind Kind = LayerKind.Vector, RasterSettings? Raster = null,PassOrderMode PassOrder=PassOrderMode.WholeLayer,bool KeepEmpty=false);
public sealed record DesignDocument(int Version, string Name, double Width, double Height, List<DesignShape> Shapes, List<CutLayer> Layers)
{
    public static DesignDocument Empty(string name = "Untitled") => new(1, name, 400, 400, [], [new("Layer 1", "#4169E1")]);
    public void Validate()
    {
        if (Version != 1) throw new InvalidDataException("Unsupported project version.");
        if (!FinitePositive(Width) || !FinitePositive(Height) || Width > 10000 || Height > 10000) throw new InvalidDataException("Invalid bed size.");
        if (string.IsNullOrWhiteSpace(Name) || Name.Length > 256 || Shapes is null || Layers is null || Layers.Count == 0 || Layers.Count > 256 || Shapes.Count > 100000 || Shapes.Any(s => s is null) || Layers.Any(l => l is null)) throw new InvalidDataException("Invalid project contents.");
        if (Shapes.Select(s => s.Id).Distinct().Count() != Shapes.Count) throw new InvalidDataException("Duplicate object IDs.");
        foreach (var s in Shapes)
        {
            if (s.GroupId == Guid.Empty) throw new InvalidDataException("Group IDs must be nonempty or absent.");
            if (!Enum.IsDefined(s.Kind) || s.Id == Guid.Empty || !double.IsFinite(s.X) || !double.IsFinite(s.Y) || Math.Abs(s.X) > 10000 || Math.Abs(s.Y) > 10000 || !double.IsFinite(s.Width) || !double.IsFinite(s.Height) || s.Width < 0 || s.Height < 0 || (s.Kind != ShapeKind.Path && (s.Width == 0 || s.Height == 0)) || s.Width > 10000 || s.Height > 10000 || s.Layer < 0 || s.Layer >= Layers.Count)
                throw new InvalidDataException("Invalid shape geometry or layer.");
            if (s.Kind == ShapeKind.Image) (s.Image ?? throw new InvalidDataException("Image object has no source asset.")).Validate();
            else if (s.Image is not null) throw new InvalidDataException("Only image objects can contain raster assets.");
            if (s.Kind == ShapeKind.Path) VectorPaths.Validate(s); else if (s.Path is not null) throw new InvalidDataException("Only paths can contain vertices.");
            if (s.Text is not null)
            {
                s.Text.Validate();
                if (s.Kind != ShapeKind.Path || s.Width <= 0 || s.Height <= 0 || s.Path!.Contours.Any(c => !c.Closed)) throw new InvalidDataException("Editable text requires nonzero closed cached outlines.");
            }
            if ((s.Kind == ShapeKind.Image) != (Layers[s.Layer].Kind == LayerKind.Image)) throw new InvalidDataException("Images and vectors require separate layer types.");
        }
        if (Shapes.Where(s => s.Path is not null).Sum(s => (long)s.Path!.Contours.Sum(c => c.Points.Length)) > 200000) throw new InvalidDataException("Project exceeds the 200000 path-vertex budget.");
        if (Shapes.Count(s => s.Kind == ShapeKind.Image) > 128 || Shapes.Where(s => s.Image is not null).Sum(s => (long)s.Image!.DataBase64.Length) > 24_000_000 || Shapes.Where(s => s.Image is not null).Select(s => s.Image!).DistinctBy(a => a.Id).Sum(a => (long)a.PixelWidth * a.PixelHeight) > 80_000_000) throw new InvalidDataException("Embedded image data exceeds the project limit.");
        if (Shapes.Where(s => s.Image is not null).GroupBy(s => s.Image!.Id).Any(g => g.Select(s => s.Image).Distinct().Count() > 1)) throw new InvalidDataException("Conflicting embedded image asset IDs.");
        if (Shapes.Where(s => s.Kind == ShapeKind.Image).GroupBy(s => s.Layer).Any(g => g.Count() > 1)) throw new InvalidDataException("Each image requires its own image layer.");
        foreach (var l in Layers)
            if (string.IsNullOrWhiteSpace(l.Name) || !System.Text.RegularExpressions.Regex.IsMatch(l.Color ?? "", "^#[0-9a-fA-F]{6}$") || !FinitePositive(l.Speed) || l.Speed > 100000 || !double.IsFinite(l.Power) || l.Power < 0 || l.Power > 100 || !Enum.IsDefined(l.Mode) || !Enum.IsDefined(l.PassOrder) || l.Passes < 1 || l.Passes > 100 || !FinitePositive(l.Interval) || l.Interval < .01 || l.Interval > 100) throw new InvalidDataException("Invalid cut settings.");
        foreach (var l in Layers) if (!Enum.IsDefined(l.Kind) || (l.Kind == LayerKind.Image) != (l.Mode == CutMode.Image)) throw new InvalidDataException("Image layers require Image mode; vector layers require Line or Fill mode.");
        foreach (var l in Layers) l.Raster?.Validate();
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
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { JsonSerializer.Serialize(stream, document, Options); if (stream.Length > 32 * 1024 * 1024) throw new InvalidDataException("Project exceeds the 32 MB limit."); stream.Flush(true); }
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
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
            var e = s.Kind == ShapeKind.Image
                ? new XElement(ns + "image", new XAttribute("x", N(s.X)), new XAttribute("y", N(s.Y)), new XAttribute("width", N(s.Width)), new XAttribute("height", N(s.Height)), new XAttribute("href", $"data:{s.Image!.MimeType};base64,{s.Image.DataBase64}"))
                : s.Kind == ShapeKind.Path
                ? new XElement(ns + "path", new XAttribute("d", string.Join(" ", VectorPaths.World(s).Select(c => "M " + string.Join(" L ", c.Points.Select(p => N(p.X) + " " + N(p.Y))) + (c.Closed ? " Z" : "")))))
                : s.Kind == ShapeKind.Rectangle
                ? new XElement(ns + "rect", new XAttribute("x", N(s.X)), new XAttribute("y", N(s.Y)), new XAttribute("width", N(s.Width)), new XAttribute("height", N(s.Height)))
                : new XElement(ns + "ellipse", new XAttribute("cx", N(s.X + s.Width / 2)), new XAttribute("cy", N(s.Y + s.Height / 2)), new XAttribute("rx", N(s.Width / 2)), new XAttribute("ry", N(s.Height / 2)));
            if (s.Kind == ShapeKind.Image && (s.FlipX || s.FlipY)) e.Add(new XAttribute("transform", $"matrix({(s.FlipX ? -1 : 1)} 0 0 {(s.FlipY ? -1 : 1)} {N(s.FlipX ? 2 * s.X + s.Width : 0)} {N(s.FlipY ? 2 * s.Y + s.Height : 0)})"));
            if (s.Kind != ShapeKind.Image) e.Add(new XAttribute("fill", "none"), new XAttribute("stroke", d.Layers[s.Layer].Color), new XAttribute("stroke-width", "0.3")); root.Add(e);
        }
        return root.ToString();
    }
}
