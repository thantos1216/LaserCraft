using LaserCraft.Domain;
using SkiaSharp;

namespace LaserCraft.Desktop;

public static class FontOutlines
{
    public static string[] Families() => SKFontManager.Default.FontFamilies.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
    public static DesignShape Create(TextSource source, int layer = 0, CancellationToken cancellationToken = default)
    {
        source.Validate(); cancellationToken.ThrowIfCancellationRequested();
        if (!Families().Contains(source.Family, StringComparer.OrdinalIgnoreCase)) throw new InvalidDataException("The selected font is not installed. Cached outlines remain usable; choose an installed font to edit text.");
        using var face = SKTypeface.FromFamilyName(source.Family, source.Bold ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal, SKFontStyleWidth.Normal, source.Italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright);
        if (face is null || !string.Equals(face.FamilyName, source.Family, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Requested font could not be resolved without substitution.");
        if (source.Bold != (face.FontStyle.Weight >= 600) || source.Italic != (face.FontStyle.Slant != SKFontStyleSlant.Upright)) throw new InvalidDataException("Requested font style is not available in this family.");
        using var font = new SKFont(face, (float)source.EmHeight); using var combined = new SKPath();
        var lines = source.Content.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested(); if (lines[i].Length == 0) continue;
            if (font.GetGlyphs(lines[i]).Any(g => g == 0)) throw new InvalidDataException("The selected font does not contain every requested character.");
            using var path = font.GetTextPath(lines[i], new SKPoint(0, (float)(i * source.EmHeight * source.LineSpacing)));
            combined.AddPath(path); if (combined.VerbCount > 100000) throw new InvalidDataException("Text outline complexity exceeds the path budget.");
        }
        if (combined.IsEmpty) throw new InvalidDataException("Text has no drawable outlines.");
        var shape = SvgPaths.Read(combined.ToSvgPathData(), layer, curveTolerance: source.Tolerance, cancellationToken: cancellationToken);
        if (shape.Path!.Contours.Any(c => !c.Closed)) throw new InvalidDataException("Font produced open outlines; choose a different font.");
        var result = shape with { X = 0, Y = 0, Text = source with { NaturalWidth = shape.Width, NaturalHeight = shape.Height } };
        result.Text.Validate(); return result;
    }
}
