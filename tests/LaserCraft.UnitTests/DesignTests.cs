using LaserCraft.Domain;
using System.Xml.Linq;

namespace LaserCraft.UnitTests;

public sealed class DesignTests
{
    private static DesignDocument Example() => DesignDocument.Empty() with { Shapes = [new(Guid.NewGuid(), ShapeKind.Rectangle, 10, 20, 30, 40)] };
    [Fact]
    public void HistoryRestoresGeometryAndInvalidatesRedo()
    {
        var h = new EditorHistory(); h.Apply(Example()); h.Undo(); Assert.Empty(h.Document.Shapes);
        h.Redo(); Assert.Single(h.Document.Shapes); h.Undo(); h.Apply(DesignDocument.Empty("Other")); h.Redo(); Assert.Equal("Other", h.Document.Name);
    }
    [Fact]
    public void ProjectsRoundTripAndOverwriteAtomically()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".lcraft");
        try { var d = Example(); ProjectFile.Save(path, d); Assert.Equal(d.Shapes[0], ProjectFile.Load(path).Shapes[0]); ProjectFile.Save(path, DesignDocument.Empty()); Assert.Empty(ProjectFile.Load(path).Shapes); }
        finally { File.Delete(path); }
    }
    [Fact] public void InvalidGeometryIsRejected() => Assert.Throws<InvalidDataException>(() => (Example() with { Width = double.NaN }).Validate());
    [Fact] public void InvalidLayerIsRejected() => Assert.Throws<InvalidDataException>(() => (Example() with { Shapes = [new(Guid.NewGuid(), ShapeKind.Rectangle, 0, 0, 10, 10, 42)] }).Validate());
    [Fact]
    public void SvgPreservesMillimetresAndGeometry()
    {
        var root = XElement.Parse(SvgExport.Write(Example())); Assert.Equal("400mm", root.Attribute("width")!.Value);
        Assert.Equal("30", root.Elements().Single().Attribute("width")!.Value);
    }
    [Fact] public void FutureVersionIsRejected() => Assert.Throws<InvalidDataException>(() => (Example() with { Version = 2 }).Validate());
    [Fact] public void NullShapesAreRejected() => Assert.Throws<InvalidDataException>(() => (Example() with { Shapes = [null!] }).Validate());
    [Fact] public void ExtremeCoordinatesAreRejected() => Assert.Throws<InvalidDataException>(() => (Example() with { Shapes = [new(Guid.NewGuid(), ShapeKind.Ellipse, double.MaxValue, 0, 2, 2)] }).Validate());
    [Fact] public void LayerColoursMustBeHex() => Assert.Throws<InvalidDataException>(() => (Example() with { Layers = [new("Cut", "invalid")] }).Validate());
}
