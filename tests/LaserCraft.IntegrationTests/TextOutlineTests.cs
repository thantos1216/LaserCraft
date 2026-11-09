using LaserCraft.Domain;
using LaserCraft.Desktop;

namespace LaserCraft.IntegrationTests;

public sealed class TextOutlineTests
{
    [Fact]
    public void InstalledFontProducesPortableClosedOutlinesWithHolesAndOutput()
    {
        var source = new TextSource("BO 18\nCafé", "Arial", 10, Tolerance: .02);
        var shape = FontOutlines.Create(source) with { X = 20, Y = 30 };
        Assert.NotNull(shape.Text); Assert.All(shape.Path!.Contours, c => Assert.True(c.Closed)); Assert.True(shape.Path.Contours.Length > 9);
        var d = TextEditing.Install(DesignDocument.Empty(), shape); var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".lcraft");
        try
        {
            ProjectFile.Save(path, d); var loaded = ProjectFile.Load(path); Assert.Equal(shape.Text, loaded.Shapes[0].Text);
            var line = ToolpathPlanner.Build(loaded); Assert.True(line.CutLength > 0);
            var fillDoc = loaded with { Layers = [loaded.Layers[0] with { Mode = CutMode.Fill, Interval = .5 }] }; Assert.True(ToolpathPlanner.Build(fillDoc).CutLength > 0);
            Assert.EndsWith("M5\nM9\n", GrblExport.Write(line, DeviceProfile.Simulator()));
            var svg = Assert.Single(SvgImport.Read(SvgExport.Write(loaded))); Assert.Null(svg.Text); Assert.Equal(shape.Path.Contours.Length, svg.Path!.Contours.Length);
        }
        finally { File.Delete(path); }
    }
    [Fact]
    public void FontStylesAndMultilineSpacingAffectPreparedGeometry()
    {
        var regular = FontOutlines.Create(new("Laser", "Arial", 10)); var bold = FontOutlines.Create(new("Laser", "Arial", 10, Bold: true)); var italic = FontOutlines.Create(new("Laser", "Arial", 10, Italic: true));
        Assert.NotEqual(regular.Width, bold.Width); Assert.NotEqual(regular.Width, italic.Width);
        var low = FontOutlines.Create(new("A\nA", "Arial", 10, LineSpacing: 1)); var high = FontOutlines.Create(new("A\nA", "Arial", 10, LineSpacing: 2)); Assert.Equal(10, high.Height - low.Height, 4);
        Assert.True(FontOutlines.Create(new("BO", "Arial", 10, Tolerance: .01)).Path!.Contours.Sum(c => c.Points.Length) >= FontOutlines.Create(new("BO", "Arial", 10, Tolerance: .5)).Path!.Contours.Sum(c => c.Points.Length));
    }
    [Fact]
    public void MissingFontsAndCancellationDoNotProduceSubstitutedGeometry()
    {
        Assert.Throws<InvalidDataException>(() => FontOutlines.Create(new("Test", "LaserCraft missing font 123456789")));
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); Assert.ThrowsAny<OperationCanceledException>(() => FontOutlines.Create(new("Test", "Arial"), cancellationToken: cancel.Token));
    }
}
