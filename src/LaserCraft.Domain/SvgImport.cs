using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using System.Text.RegularExpressions;

namespace LaserCraft.Domain;

public static class SvgImport
{
    public static List<DesignShape> Read(string text, int layer = 0)
    {
        if (layer < 0 || layer >= 256) throw new InvalidDataException("Invalid target SVG layer.");
        using var reader = XmlReader.Create(new StringReader(text), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 4_000_000 });
        var root = XDocument.Load(reader).Root ?? throw new InvalidDataException("Empty SVG.");
        if (root.Name.LocalName != "svg") throw new InvalidDataException("Expected SVG root.");
        var box = (root.Attribute("viewBox")?.Value ?? "").Split([' ', ',', '\t', '\n'], StringSplitOptions.RemoveEmptyEntries).Select(N).ToArray();
        if (box.Length != 0 && (box.Length != 4 || box[2] <= 0 || box[3] <= 0)) throw new InvalidDataException("Invalid SVG viewBox.");
        var w = Length(root.Attribute("width")?.Value, box.Length == 4 ? box[2] : 300);
        var h = Length(root.Attribute("height")?.Value, box.Length == 4 ? box[3] : 150);
        var sx = box.Length == 4 ? w / box[2] : 25.4 / 96; var sy = box.Length == 4 ? h / box[3] : 25.4 / 96;
        if (Math.Abs(sx - sy) > 1e-8) throw new InvalidDataException("Non-uniform SVG viewBox scaling is not supported yet.");
        var ox = box.Length == 4 ? box[0] : 0; var oy = box.Length == 4 ? box[1] : 0;
        var result = new List<DesignShape>();
        foreach (var e in root.DescendantsAndSelf())
        {
            if (e.Attribute("transform") is not null || e.Attribute("style") is not null) throw new InvalidDataException("SVG transforms and CSS styles are not supported yet.");
            double A(string name, double fallback = 0) => e.Attribute(name) is { } a ? N(a.Value) : fallback;
            DesignShape? s = e.Name.LocalName switch
            {
                "svg" or "g" or "title" or "desc" => null,
                "rect" when A("rx") == 0 && A("ry") == 0 => new(Guid.NewGuid(), ShapeKind.Rectangle, (A("x") - ox) * sx, (A("y") - oy) * sy, A("width") * sx, A("height") * sy, layer),
                "ellipse" => new(Guid.NewGuid(), ShapeKind.Ellipse, (A("cx") - A("rx") - ox) * sx, (A("cy") - A("ry") - oy) * sy, 2 * A("rx") * sx, 2 * A("ry") * sy, layer),
                "circle" => new(Guid.NewGuid(), ShapeKind.Ellipse, (A("cx") - A("r") - ox) * sx, (A("cy") - A("r") - oy) * sy, 2 * A("r") * sx, 2 * A("r") * sy, layer),
                "path" => SvgLinearPaths.Read(e.Attribute("d")?.Value ?? "", layer, sx, sy, ox, oy),
                "polyline" or "polygon" => SvgLinearPaths.Read("M " + (e.Attribute("points")?.Value ?? "") + (e.Name.LocalName == "polygon" ? " Z" : ""), layer, sx, sy, ox, oy),
                "line" => VectorPaths.Create([new([new((A("x1") - ox) * sx, (A("y1") - oy) * sy), new((A("x2") - ox) * sx, (A("y2") - oy) * sy)])], layer),
                _ => throw new InvalidDataException($"SVG element '{e.Name.LocalName}' is not supported yet. No shapes were imported.")
            };
            if (s is not null) result.Add(s);
        }
        (DesignDocument.Empty() with { Shapes = result, Layers = Enumerable.Range(0, layer + 1).Select(i => new CutLayer($"Layer {i + 1}", "#4169E1")).ToList() }).Validate(); return result;
    }
    private static double N(string value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || !double.IsFinite(number)) throw new InvalidDataException("SVG contains an invalid numeric coordinate.");
        return number;
    }
    private static double Length(string? value, double fallback)
    {
        if (value is null) return fallback * 25.4 / 96;
        var match = Regex.Match(value.Trim(), "^([+0-9.eE-]+)(mm|cm|in|px)?$"); if (!match.Success) throw new InvalidDataException("Unsupported SVG length unit.");
        var amount = N(match.Groups[1].Value); if (amount <= 0) throw new InvalidDataException("SVG dimensions must be positive.");
        return amount * (match.Groups[2].Value switch { "mm" => 1, "cm" => 10, "in" => 25.4, _ => 25.4 / 96 });
    }
}
