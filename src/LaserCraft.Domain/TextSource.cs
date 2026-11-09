namespace LaserCraft.Domain;

public sealed record TextSource(string Content, string Family, double EmHeight = 10, bool Bold = false, bool Italic = false, double LineSpacing = 1.2, double Tolerance = .05, double NaturalWidth = 1, double NaturalHeight = 1)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Content) || Content.Length > 512 || Content.Count(c => c == '\n') > 31 || Content.Any(c => c != '\n' && (char.IsControl(c) || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.Format || c < ' ' || c > '\u024f')))
            throw new InvalidDataException("Basic text supports up to 512 Latin characters and 32 lines. Use spaces instead of tabs. Complex scripts and combining sequences are not supported.");
        if (string.IsNullOrWhiteSpace(Family) || Family.Length > 128 || Family.Any(char.IsControl)) throw new InvalidDataException("Choose a font family.");
        if (!double.IsFinite(EmHeight) || EmHeight < .1 || EmHeight > 1000 || !double.IsFinite(LineSpacing) || LineSpacing < .5 || LineSpacing > 5) throw new InvalidDataException("Font em height must be 0.1–1000 mm and line spacing 0.5–5.");
        SvgPaths.ValidateTolerance(Tolerance);
        if (!double.IsFinite(NaturalWidth) || !double.IsFinite(NaturalHeight) || NaturalWidth <= 0 || NaturalHeight <= 0 || NaturalWidth > 10000 || NaturalHeight > 10000) throw new InvalidDataException("Invalid cached text dimensions.");
    }
}
public static class TextEditing
{
    public static DesignDocument Install(DesignDocument document, DesignShape prepared, Guid? existingId = null)
    {
        document.Validate(); prepared.Text?.Validate();
        if (prepared.Kind != ShapeKind.Path || prepared.Text is null) throw new InvalidDataException("Text needs prepared closed outlines and source settings.");
        DesignShape next = prepared;
        if (existingId is Guid id)
        {
            var old = document.Shapes.SingleOrDefault(s => s.Id == id && s.Text is not null) ?? throw new InvalidDataException("The selected text object is no longer available.");
            next = prepared with { Id = old.Id, Layer = old.Layer, X = old.X, Y = old.Y, Width = prepared.Width * old.Width / old.Text!.NaturalWidth, Height = prepared.Height * old.Height / old.Text.NaturalHeight, FlipX = old.FlipX, FlipY = old.FlipY };
        }
        var result = document with { Shapes = existingId is null ? [.. document.Shapes, next] : document.Shapes.Select(s => s.Id == existingId ? next : s).ToList() }; result.Validate(); return result;
    }
}
