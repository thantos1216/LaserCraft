using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class TextEditingTests
{
    private static DesignShape Prepared(string content = "Test", double width = 10, double height = 5) => VectorPaths.Convert(new(Guid.NewGuid(), ShapeKind.Rectangle, 0, 0, width, height)) with { Text = new(content, "A cached font that need not be installed", NaturalWidth: width, NaturalHeight: height) };
    [Theory]
    [InlineData("")] [InlineData("   ")] [InlineData("a\tb")] [InlineData("a\rb")] [InlineData("مرحبا")] [InlineData("e\u0301")] [InlineData("🙂")] [InlineData("a\u0085b")] [InlineData("a\u00adb")]
    public void UnsupportedBasicTextIsRejectedExplicitly(string value) => Assert.Throws<InvalidDataException>(() => new TextSource(value, "Arial").Validate());
    [Fact]
    public void TextLimitsAndNonfiniteMetricsAreValidated()
    {
        new TextSource("Café 123\nLine 2", "Arial").Validate();
        foreach (var text in new[] { new string('A', 513), string.Join('\n', Enumerable.Repeat("A", 33)) }) Assert.Throws<InvalidDataException>(() => new TextSource(text, "Arial").Validate());
        foreach (var source in new[] { new TextSource("A", ""), new("A", "Arial", double.NaN), new("A", "Arial", 1001), new("A", "Arial", LineSpacing: 0), new("A", "Arial", Tolerance: 0), new("A", "Arial", NaturalWidth: 0), new("A", "Arial", NaturalHeight: double.PositiveInfinity) }) Assert.Throws<InvalidDataException>(source.Validate);
    }
    [Fact]
    public void EditingRetainsIdentityLayerPositionScalingAndMirrors()
    {
        var old = Prepared() with { X = 25, Y = 30, Width = 20, Height = 15, FlipX = true, FlipY = true };
        var d = DesignDocument.Empty() with { Shapes = [old] }; var fresh = Prepared("New", 15, 4);
        var next = TextEditing.Install(d, fresh, old.Id); var updated = Assert.Single(next.Shapes);
        Assert.Equal(old.Id, updated.Id); Assert.Equal(old.Layer, updated.Layer); Assert.Equal((25d, 30d, 30d, 12d), (updated.X, updated.Y, updated.Width, updated.Height));
        Assert.True(updated.FlipX); Assert.True(updated.FlipY); Assert.Equal("New", updated.Text!.Content); Assert.Equal("Test", d.Shapes[0].Text!.Content);
        var again = TextEditing.Install(next, Prepared("Again", 5, 2), old.Id).Shapes[0]; Assert.Equal(10, again.Width); Assert.Equal(6, again.Height);
    }
    [Fact]
    public void CachedTextSavesLoadsAndPlansWithoutResolvingItsFont()
    {
        var text = Prepared() with { X = 10, Y = 10 }; var d = TextEditing.Install(DesignDocument.Empty(), text); var history = new EditorHistory(); history.Reset(d);
        history.Apply(TextEditing.Install(d, Prepared("Edited"), text.Id)); history.Undo(); Assert.Equal("Test", history.Document.Shapes[0].Text!.Content); history.Redo(); Assert.Equal("Edited", history.Document.Shapes[0].Text!.Content);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".lcraft");
        try { ProjectFile.Save(path, d); var loaded = ProjectFile.Load(path); Assert.Equal(text.Text, loaded.Shapes[0].Text); Assert.Equal(30, ToolpathPlanner.Build(loaded).CutLength); }
        finally { File.Delete(path); }
    }
    [Fact]
    public void AffineCopiesRetainTextWhileTopologyEditsDetachIt()
    {
        var text = Prepared() with { X = 10, Y = 10 }; var d = DesignDocument.Empty() with { Shapes = [text] }; var ids = new HashSet<Guid> { text.Id };
        var array = Arrangement.RectangularArray(d, ids, 1, 2, 5, 0).Document; Assert.All(array.Shapes, s => Assert.Same(text.Text, s.Text));
        Assert.Same(text.Text, ShapeEditing.Mirror(d, ids, true).Shapes[0].Text);
        Assert.Null(VectorPaths.Convert(text).Text); Assert.Null(PathNodeEditing.InsertAfter(text, 0, 0).Text);
        Assert.Null(Arrangement.RotateVectors(d, ids, 90).Shapes[0].Text); Assert.Null(Arrangement.ReversePaths(d, ids).Shapes[0].Text);
        var separate = PathComposition.Separate(d with { Shapes = [text with { Path = new([text.Path!.Contours[0], text.Path.Contours[0]]) }] }, ids); Assert.All(separate.Document.Shapes, s => Assert.Null(s.Text));
    }
    [Fact]
    public void InvalidMetadataAndStaleEditsFailBeforeChangingTheSource()
    {
        var text = Prepared(); var d = TextEditing.Install(DesignDocument.Empty(), text);
        Assert.Throws<InvalidDataException>(() => TextEditing.Install(d, text, Guid.NewGuid()));
        Assert.Throws<InvalidDataException>(() => TextEditing.Install(d, text with { Text = null }));
        Assert.Throws<InvalidDataException>(() => (d with { Shapes = [text with { Kind = ShapeKind.Rectangle, Path = null }] }).Validate());
        Assert.Throws<InvalidDataException>(() => (d with { Shapes = [text with { Path = new([text.Path!.Contours[0] with { Closed = false }]) }] }).Validate());
        Assert.Throws<InvalidDataException>(() => (d with { Shapes = [text with { Width = 0 }] }).Validate()); Assert.Single(d.Shapes);
    }
}
