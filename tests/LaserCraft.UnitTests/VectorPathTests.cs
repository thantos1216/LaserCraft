using LaserCraft.Domain;
using System.Collections.Immutable;

namespace LaserCraft.UnitTests;

public sealed class VectorPathTests
{
    private static DesignDocument Document(DesignShape shape, CutMode mode = CutMode.Line)
    { var d = DesignDocument.Empty(); d.Shapes.Add(shape); d.Layers[0] = d.Layers[0] with { Mode = mode, Interval = 1 }; return d; }
    private static PathContour Square(double x, double y, double side) => new([new(x, y), new(x + side, y), new(x + side, y + side), new(x, y + side)], true);
    [Fact]
    public void OpenAndClosedContoursHaveDifferentCutLengths()
    {
        var closed = VectorPaths.Create([Square(10, 20, 10)]); var open = closed with { Path = new([closed.Path!.Contours[0] with { Closed = false }]) };
        Assert.Equal(40, ToolpathPlanner.Build(Document(closed)).CutLength, 8); Assert.Equal(30, ToolpathPlanner.Build(Document(open)).CutLength, 8);
    }
    [Fact]
    public void VerticalAndHorizontalLinesRetainZeroExtent()
    {
        foreach (var line in new[] { "M10 10V40", "M10 10H40" }) { var s = SvgLinearPaths.Read(line); Document(s).Validate(); Assert.Equal(30, ToolpathPlanner.Build(Document(s)).CutLength, 8); }
    }
    [Fact]
    public void RelativeImplicitAndHorizontalVerticalCommandsMatchAbsoluteGeometry()
    {
        var a = SvgLinearPaths.Read("m10,20 10,0 v10 h-10 z"); var b = SvgLinearPaths.Read("M10 20 L20 20 20 30 10 30 Z");
        Assert.Equal(VectorPaths.World(a)[0].Points.ToArray(), VectorPaths.World(b)[0].Points.ToArray()); Assert.True(a.Path!.Contours[0].Closed);
    }
    [Fact]
    public void CompoundFillLeavesAHole()
    {
        var s = VectorPaths.Create([Square(10, 10, 10), Square(13, 13, 4)]); var d = Document(s, CutMode.Fill); var plan = ToolpathPlanner.Build(d);
        Assert.Equal(84, plan.CutLength, 8);
        Assert.DoesNotContain(plan.Segments, p => !p.Travel && p.From.Y > 13 && p.From.Y < 17 && Math.Min(p.From.X, p.To.X) < 15 && Math.Max(p.From.X, p.To.X) > 15);
        Assert.False(ShapeEditing.HitTest(d, s, 15, 15, .1)); Assert.True(ShapeEditing.HitTest(d, s, 11, 11, .1));
    }
    [Fact] public void FillRejectsAnOpenContour() => Assert.Throws<InvalidDataException>(() => ToolpathPlanner.Build(Document(SvgLinearPaths.Read("M10 10L30 10 30 30"), CutMode.Fill)));
    [Fact]
    public void DisconnectedContoursAreJoinedOnlyByOffPowerTravel()
    {
        var s = SvgLinearPaths.Read("M10 10L20 10M30 10L40 10"); var plan = ToolpathPlanner.Build(Document(s)); Assert.Equal(20, plan.CutLength, 8);
        Assert.Contains(plan.Segments, e => e.Travel && e.From == new PointMm(20, 10) && e.To == new PointMm(30, 10) && e.Power == 0);
        Assert.EndsWith("M5\nM9\n", GrblExport.Write(plan, DeviceProfile.Simulator()));
    }
    [Fact]
    public void FillIntersectionBudgetRejectsExcessiveWorkBeforeScanning()
    {
        var s = VectorPaths.Create(Enumerable.Range(0, 100).Select(i => Square(i * 2, 10, 1))); var d = Document(s, CutMode.Fill); d.Layers[0] = d.Layers[0] with { Interval = .01 };
        // 100 rows * 400 edges is within the budget; reducing height density on taller geometry is not.
        var tall = s with { Height = 100 }; Assert.Throws<InvalidDataException>(() => ToolpathPlanner.Build(Document(tall, CutMode.Fill) with { Layers = [d.Layers[0]] }));
    }
    [Fact]
    public void MirroringAndResizingTransformActualVertices()
    {
        var s = SvgLinearPaths.Read("M10 10L30 20 10 40"); var d = Document(s); var mirrored = ShapeEditing.Mirror(d, new HashSet<Guid> { s.Id }, true).Shapes[0];
        Assert.Equal(new PointMm(30, 10), VectorPaths.World(mirrored)[0].Points[0]);
        Assert.Equal(new PointMm(10, 20), VectorPaths.World(mirrored)[0].Points[1]);
        Assert.Equal(new PointMm(10, 70), VectorPaths.World(s with { Width = 40, Height = 60 })[0].Points[2]);
        Assert.Equal(new PointMm(50, 30), VectorPaths.World(s with { Width = 40, Height = 60 })[0].Points[1]);
    }
    [Fact]
    public void NodeMovesRenormalizeWithoutChangingOtherVerticesOrIdentity()
    {
        var s = SvgLinearPaths.Read("M10 10L30 10 30 30Z"); var changed = VectorPaths.MoveNode(s, 0, 0, new(5, 8));
        Assert.Equal(s.Id, changed.Id); Assert.Equal(new PointMm(5, 8), VectorPaths.World(changed)[0].Points[0]); Assert.Equal(new PointMm(30, 10), VectorPaths.World(changed)[0].Points[1]);
        Assert.Equal(new PointMm(10, 10), VectorPaths.World(s)[0].Points[0]);
        var history = new EditorHistory(); history.Reset(Document(s)); history.Apply(Document(changed)); history.Undo(); Assert.Equal(new PointMm(10, 10), VectorPaths.World(history.Document.Shapes[0])[0].Points[0]); history.Redo(); Assert.Equal(new PointMm(5, 8), VectorPaths.World(history.Document.Shapes[0])[0].Points[0]);
    }
    [Fact]
    public void NativeAndSvgRoundTripsRetainCompoundGeometry()
    {
        var s = VectorPaths.Create([Square(10, 10, 10), Square(13, 13, 4)]); var d = Document(s); var path = Path.GetTempFileName();
        try { ProjectFile.Save(path, d); var loaded = ProjectFile.Load(path); Assert.Equal(2, loaded.Shapes[0].Path!.Contours.Length); var imported = Assert.Single(SvgImport.Read(SvgExport.Write(loaded))); Assert.Equal(56, ToolpathPlanner.Build(Document(imported)).CutLength, 6); }
        finally { File.Delete(path); }
    }
    [Fact]
    public void SvgImportsPolygonsPolylinesAndLinesWithViewboxScaling()
    {
        var shapes = SvgImport.Read("<svg width='100mm' height='100mm' viewBox='0 0 100 100'><polygon points='10,10 20,10 20,20'/><polyline points='30,30 40,30'/><line x1='50' y1='50' x2='50' y2='60'/></svg>");
        Assert.Equal(3, shapes.Count); Assert.True(shapes[0].Path!.Contours[0].Closed); Assert.False(shapes[1].Path!.Contours[0].Closed); Assert.Equal(0, shapes[2].Width);
    }
    [Theory]
    [InlineData("M0 0 C1 1 2 2 3 3")]
    [InlineData("M0 0 Q1 1 2 2")]
    [InlineData("M0 0 A1 1 0 0 0 2 2")]
    [InlineData("L1 1")]
    [InlineData("M1")]
    [InlineData("M1 1L")]
    [InlineData("M1 1L2 2@")]
    [InlineData("M1 1Z")]
    public void InvalidOrUnsupportedPathsFailAtomically(string data) => Assert.Throws<InvalidDataException>(() => SvgLinearPaths.Read(data));
    [Fact]
    public void StrokeHitTestingDoesNotSelectEmptyBoundingBoxInterior()
    {
        var s = SvgLinearPaths.Read("M10 10L30 30"); var d = Document(s); Assert.True(ShapeEditing.HitTest(d, s, 20, 20, .5)); Assert.False(ShapeEditing.HitTest(d, s, 11, 29, .5));
    }
    [Fact]
    public void ShapesConvertToEditableClosedContours()
    {
        var rectangle = new DesignShape(Guid.NewGuid(), ShapeKind.Rectangle, 10, 20, 30, 40); var path = VectorPaths.Convert(rectangle); Assert.Equal(rectangle.Id, path.Id); Assert.Equal(140, ToolpathPlanner.Build(Document(path)).CutLength, 8); Assert.Equal(128, VectorPaths.Convert(rectangle with { Kind = ShapeKind.Ellipse }).Path!.Contours[0].Points.Length);
    }
    [Fact]
    public void InvalidNormalizedVerticesAreRejected()
    {
        var s = SvgLinearPaths.Read("M10 10L30 30"); var bad = s with { Path = new([new([new(double.NaN, 0), new(1, 1)])]) }; Assert.Throws<InvalidDataException>(() => Document(bad).Validate());
        Assert.Throws<InvalidDataException>(() => Document(s with { Width = -1 }).Validate());
    }
}
