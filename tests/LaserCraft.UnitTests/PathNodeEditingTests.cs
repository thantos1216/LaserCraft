using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class PathNodeEditingTests
{
    private static DesignShape Square() => SvgLinearPaths.Read("M10 10H30V30H10Z");
    private static DesignDocument Document(DesignShape s) { var d = DesignDocument.Empty(); d.Shapes.Add(s); return d; }
    [Fact]
    public void InsertingOnClosingEdgeRetainsLengthAndIdentity()
    {
        var s = Square(); var inserted = PathNodeEditing.InsertAfter(s, 0, 3);
        Assert.Equal(s.Id, inserted.Id); Assert.Equal(s.Layer, inserted.Layer);
        Assert.Equal(new PointMm(10, 20), VectorPaths.World(inserted)[0].Points[4]);
        Assert.Equal(80, ToolpathPlanner.Build(Document(inserted)).CutLength, 8);
        Assert.Equal(4, s.Path!.Contours[0].Points.Length);
    }
    [Fact]
    public void MirroredNodeEditsUseDisplayedWorldCoordinates()
    {
        var s = Square() with { FlipX = true, FlipY = true, Layer = 2 };
        var inserted = PathNodeEditing.InsertAfter(s, 0, 0, new(20, 30));
        Assert.Equal(new PointMm(20, 30), VectorPaths.World(inserted)[0].Points[1]);
        Assert.Equal(VectorPaths.World(s)[0].Points[0], VectorPaths.World(inserted)[0].Points[0]);
        Assert.Equal(2, inserted.Layer); Assert.False(inserted.FlipX); Assert.False(inserted.FlipY);
    }
    [Fact]
    public void RemovingNodeChangesOnlyItsContourAndIsUndoable()
    {
        var s = SvgLinearPaths.Read("M10 10H30V30H10ZM15 15H20V20H15Z"); var edited = PathNodeEditing.Remove(s, 0, 1);
        Assert.Equal(3, edited.Path!.Contours[0].Points.Length);
        Assert.Equal(VectorPaths.World(s)[1].Points.ToArray(), VectorPaths.World(edited)[1].Points.ToArray());
        var h = new EditorHistory(); h.Reset(Document(s)); h.Apply(Document(edited)); h.Undo(); Assert.Equal(s, h.Document.Shapes[0]); h.Redo(); Assert.Equal(edited, h.Document.Shapes[0]);
    }
    [Fact]
    public void BreakingClosedContourRetainsEveryCutSegmentButDisablesFill()
    {
        var s = Square(); var edited = PathNodeEditing.Break(s, 0, 2); var c = VectorPaths.World(edited)[0];
        Assert.False(c.Closed); Assert.Equal(5, c.Points.Length); Assert.Equal(new PointMm(30, 30), c.Points[0]); Assert.Equal(c.Points[0], c.Points[^1]);
        Assert.Equal(80, ToolpathPlanner.Build(Document(edited)).CutLength, 8);
        var d = Document(edited); d.Layers[0] = d.Layers[0] with { Mode = CutMode.Fill }; Assert.Throws<InvalidDataException>(() => ToolpathPlanner.Build(d));
    }
    [Fact]
    public void SplittingOpenContourKeepsSharedEndpointAndOriginalGeometry()
    {
        var s = SvgLinearPaths.Read("M10 10H20V20H30"); var edited = PathNodeEditing.Break(s, 0, 1); var c = VectorPaths.World(edited);
        Assert.Equal(2, c.Length); Assert.Equal(c[0].Points[^1], c[1].Points[0]);
        Assert.All(c, p => Assert.False(p.Closed)); Assert.Equal(30, ToolpathPlanner.Build(Document(edited)).CutLength, 8);
    }
    [Fact]
    public void RemovingPhotoContourRetainsOtherRegionsAndRoundTrips()
    {
        var s = SvgLinearPaths.Read("M10 10H30V30H10ZM15 15H20V20H15Z"); var edited = PathNodeEditing.RemoveContour(s, 1, 0);
        Assert.Single(edited.Path!.Contours); Assert.Equal(VectorPaths.World(s)[0].Points.ToArray(), VectorPaths.World(edited)[0].Points.ToArray());
        var file = Path.GetTempFileName();
        try { ProjectFile.Save(file, Document(edited)); Assert.Single(ProjectFile.Load(file).Shapes[0].Path!.Contours); }
        finally { File.Delete(file); }
    }
    [Fact]
    public void InvalidAndDegenerateEditsLeaveSourceUntouched()
    {
        var triangle = SvgLinearPaths.Read("M10 10L30 10 20 20Z"); var line = SvgLinearPaths.Read("M10 10H30");
        Assert.Throws<InvalidDataException>(() => PathNodeEditing.Remove(triangle, 0, 0));
        Assert.Throws<InvalidDataException>(() => PathNodeEditing.Remove(line, 0, 0));
        Assert.Throws<InvalidDataException>(() => PathNodeEditing.RemoveContour(line, 0, 0));
        Assert.Throws<InvalidDataException>(() => PathNodeEditing.Break(line, 0, 0));
        Assert.Throws<InvalidDataException>(() => PathNodeEditing.InsertAfter(line, 0, 1));
        Assert.Throws<InvalidDataException>(() => PathNodeEditing.Remove(line, 4, 0));
        Assert.Throws<InvalidDataException>(() => PathNodeEditing.InsertAfter(line, 0, 0, new(double.NaN, 0)));
        var collapsing = SvgLinearPaths.Read("M10 10H30L10 10"); Assert.Throws<InvalidDataException>(() => PathNodeEditing.Remove(collapsing, 0, 1));
        Assert.Equal(2, line.Path!.Contours[0].Points.Length);
    }
}
