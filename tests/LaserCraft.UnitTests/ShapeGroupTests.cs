using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class ShapeGroupTests
{
    private static DesignShape Rect(double x, double y = 10, double width = 10) => new(Guid.NewGuid(), ShapeKind.Rectangle, x, y, width, 10);
    private static HashSet<Guid> Ids(params DesignShape[] shapes) => shapes.Select(s => s.Id).ToHashSet();
    [Fact]
    public void GroupingAndUngroupingPreserveObjectsLayersAndSourceSettings()
    {
        var a = Rect(10); var asset = new RasterAsset(Guid.NewGuid(), "image/png", "AAAA", 1, 1); var b = new DesignShape(Guid.NewGuid(), ShapeKind.Image, 40, 10, 20, 20, 1, asset, true);
        var text = VectorPaths.Convert(Rect(70)) with { Text = new("Text", "Cached font", NaturalWidth: 10, NaturalHeight: 10) };
        var d = DesignDocument.Empty() with { Shapes = [a, b, text] }; d.Layers.Add(new("Image", "#123456", Mode: CutMode.Image, Kind: LayerKind.Image));
        var grouped = ShapeGroups.Group(d, Ids(a, b, text)); var group = grouped.Document.Shapes[0].GroupId; Assert.NotNull(group);
        Assert.All(grouped.Document.Shapes, s => Assert.Equal(group, s.GroupId)); Assert.Null(d.Shapes[0].GroupId); Assert.Same(asset, grouped.Document.Shapes[1].Image); Assert.Same(text.Text, grouped.Document.Shapes[2].Text);
        var restored = ShapeGroups.Ungroup(grouped.Document, Ids(a)); Assert.Equal(d.Shapes, restored.Document.Shapes); Assert.Equal(3, restored.Selection.Count); Assert.Equal(d.Layers, restored.Document.Layers);
    }
    [Fact]
    public void ClickControlAltAndHiddenMemberSelectionAreExplicit()
    {
        var group = Guid.NewGuid(); var a = Rect(10) with { GroupId = group }; var b = Rect(30) with { GroupId = group, Layer = 1 }; var c = Rect(80);
        var d = DesignDocument.Empty() with { Shapes = [a, b, c] }; d.Layers.Add(new("Hidden", "#123456", Visible: false));
        var selected = ShapeGroups.Select(d, new HashSet<Guid>(), a.Id, false); Assert.True(selected.SetEquals(Ids(a, b)));
        Assert.Empty(ShapeGroups.Select(d, selected, b.Id, true)); Assert.True(ShapeGroups.Select(d, selected, a.Id, false, true).SetEquals(Ids(a)));
        Assert.True(ShapeGroups.Select(d, new HashSet<Guid>(), a.Id, false, true).SetEquals(Ids(a)));
        Assert.True(ShapeGroups.Select(d, selected, c.Id, true).SetEquals(Ids(a, b, c))); Assert.Empty(ShapeGroups.Select(d, selected, null, false));
        Assert.True(ShapeGroups.Select(d, selected, null, true).SetEquals(selected));
        Assert.Empty(ShapeGroups.Select(d, new HashSet<Guid> { Guid.NewGuid() }, null, true));
    }
    [Fact]
    public void GroupingMergesSelectedExistingGroupsWithoutNestingOrLosingMembers()
    {
        var a = Rect(10); var b = Rect(30); var c = Rect(60); var d = DesignDocument.Empty() with { Shapes = [a, b, c] };
        var first = ShapeGroups.Group(d, Ids(a, b)); var next = ShapeGroups.Group(first.Document, Ids(a, c));
        Assert.Equal(3, next.Selection.Count); Assert.Single(next.Document.Shapes.Select(s => s.GroupId).Distinct()); Assert.NotEqual(first.Document.Shapes[0].GroupId, next.Document.Shapes[0].GroupId);
        Assert.Throws<InvalidDataException>(() => ShapeGroups.Group(first.Document, Ids(a)));
    }
    [Fact]
    public void AlignmentAndDistributionMoveGroupBoundsAsUnits()
    {
        var a = Rect(0) with { GroupId = Guid.NewGuid() }; var b = Rect(15, 20) with { GroupId = a.GroupId }; var c = Rect(40, 40) with { GroupId = Guid.NewGuid() }; var e = Rect(55, 50) with { GroupId = c.GroupId }; var f = Rect(100, 80) with { GroupId = Guid.NewGuid() }; var g = Rect(115, 90) with { GroupId = f.GroupId };
        var d = DesignDocument.Empty() with { Shapes = [a, b, c, e, f, g] }; var ids = Ids(a, b, c, e, f, g);
        var aligned = ShapeEditing.Align(d, ids, "Top"); Assert.Equal(15, aligned.Shapes[1].X - aligned.Shapes[0].X); Assert.Equal(10, aligned.Shapes[3].Y - aligned.Shapes[2].Y); Assert.Equal(10, aligned.Shapes[2].Y);
        var spread = Arrangement.Distribute(d, ids, DistributionMode.HorizontalGaps); Assert.Equal(50, spread.Shapes[2].X); Assert.Equal(65, spread.Shapes[3].X); Assert.Same(a, spread.Shapes[0]); Assert.Same(g, spread.Shapes[^1]);
        Assert.Throws<InvalidDataException>(() => Arrangement.Distribute(d, Ids(a, b, c, e), DistributionMode.HorizontalCenters));
    }
    [Fact]
    public void ArrayAndClipboardRemappingCreateIndependentGroupsPerCopy()
    {
        var a = Rect(10) with { GroupId = Guid.NewGuid() }; var b = Rect(25) with { GroupId = a.GroupId }; var d = DesignDocument.Empty() with { Shapes = [a, b] };
        var array = Arrangement.RectangularArray(d, Ids(a, b), 1, 3, 5, 0).Document;
        Assert.Equal(3, array.Shapes.Select(s => s.GroupId).Distinct().Count()); Assert.Equal(a.GroupId, array.Shapes[0].GroupId); Assert.Equal(array.Shapes[2].GroupId, array.Shapes[3].GroupId);
        var copied = ShapeGroups.RemapCopies(new[] { a with { Id = Guid.NewGuid() }, b with { Id = Guid.NewGuid() } }); Assert.NotEqual(a.GroupId, copied[0].GroupId); Assert.Equal(copied[0].GroupId, copied[1].GroupId);
    }
    [Fact]
    public void GeometryAndTextEditsRetainTheOwningGroup()
    {
        var a = Rect(30) with { GroupId = Guid.NewGuid() }; var b = Rect(50) with { GroupId = a.GroupId }; var path = VectorPaths.Convert(a); Assert.Equal(a.GroupId, path.GroupId);
        Assert.Equal(a.GroupId, VectorPaths.MoveNode(path, 0, 0, new(29, 9)).GroupId); Assert.Equal(a.GroupId, PathNodeEditing.InsertAfter(path, 0, 0).GroupId);
        var d = DesignDocument.Empty() with { Shapes = [path, VectorPaths.Convert(b)] }; Assert.All(Arrangement.RotateVectors(d, Ids(a, b), 45).Shapes, s => Assert.Equal(a.GroupId, s.GroupId));
        Assert.Equal(a.GroupId, PathComposition.Combine(d, Ids(a, b)).Document.Shapes[0].GroupId);
        var text = path with { Text = new("Old", "Cached", NaturalWidth: 10, NaturalHeight: 10) }; var prepared = VectorPaths.Convert(Rect(0)) with { Text = text.Text with { Content = "New" }, GroupId = null };
        Assert.Equal(a.GroupId, TextEditing.Install(d with { Shapes = [text] }, prepared, text.Id).Shapes[0].GroupId);
    }
    [Fact]
    public void ValidationRejectsEmptyGroupIdsButPermitsSingletonsAndSeparateIdNamespaces()
    {
        var a = Rect(10); var b = Rect(30) with { GroupId = a.Id }; var d = DesignDocument.Empty() with { Shapes = [a, b] }; d.Validate(); Assert.Equal(2, ShapeGroups.Units(d, Ids(a, b)).Count);
        Assert.Throws<InvalidDataException>(() => (d with { Shapes = [a with { GroupId = Guid.Empty }] }).Validate());
        Assert.Throws<InvalidDataException>(() => ShapeGroups.Group(d, Ids(a))); Assert.Throws<InvalidDataException>(() => ShapeGroups.Ungroup(d, Ids(a)));
        Assert.Throws<InvalidDataException>(() => ShapeGroups.Expand(d, new HashSet<Guid> { Guid.NewGuid() }));
    }
    [Fact]
    public void NativeFilesHistoryAndOutputRetainGroupsWithoutChangingToolpaths()
    {
        var a = Rect(10); var b = Rect(30); var d = DesignDocument.Empty() with { Shapes = [a, b] }; var grouped = ShapeGroups.Group(d, Ids(a, b)).Document;
        Assert.Equal(ToolpathPlanner.Build(d).Segments.ToArray(), ToolpathPlanner.Build(grouped).Segments.ToArray());
        var history = new EditorHistory(); history.Reset(d); history.Apply(grouped); history.Undo(); Assert.All(history.Document.Shapes, s => Assert.Null(s.GroupId)); history.Redo(); Assert.NotNull(history.Document.Shapes[0].GroupId);
        var path = Path.GetTempFileName();
        try { ProjectFile.Save(path, grouped); var loaded = ProjectFile.Load(path); Assert.Equal(grouped.Shapes, loaded.Shapes); Assert.Equal(2, SvgImport.Read(SvgExport.Write(loaded)).Count); }
        finally { File.Delete(path); }
    }
}
