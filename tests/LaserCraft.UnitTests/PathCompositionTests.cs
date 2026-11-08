using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class PathCompositionTests
{
    private static DesignDocument Document(params DesignShape[] shapes) { var d = DesignDocument.Empty(); d.Shapes.AddRange(shapes); return d; }
    private static DesignShape Ring() => SvgLinearPaths.Read("M10 10H30V30H10ZM15 15H25V25H15Z");
    [Fact]
    public void SeparateRetainsWorldGeometryMirrorsLayerAndFirstIdentity()
    {
        var source = Ring() with { FlipX = true, FlipY = true }; var original = Document(source); var result = PathComposition.Separate(original, new HashSet<Guid> { source.Id });
        Assert.Equal(2, result.Document.Shapes.Count); Assert.Equal(2, result.Selection.Count); Assert.Equal(source.Id, result.Document.Shapes[0].Id); Assert.NotEqual(source.Id, result.Document.Shapes[1].Id);
        var contours = VectorPaths.World(source);
        for (int i = 0; i < 2; i++) { Assert.Equal(contours[i].Points.ToArray(), VectorPaths.World(result.Document.Shapes[i])[0].Points.ToArray()); Assert.Equal(source.Layer, result.Document.Shapes[i].Layer); Assert.True(result.Document.Shapes[i].Path!.Contours[0].Closed); }
        Assert.Single(original.Shapes); Assert.Equal(2, original.Shapes[0].Path!.Contours.Length);
    }
    [Fact]
    public void SeparationAndCombinationPreserveFillHoleAndCutLength()
    {
        var source = Ring(); var d = Document(source); d.Layers[0] = d.Layers[0] with { Mode = CutMode.Fill, Interval = 1 };
        var expected = ToolpathPlanner.Build(d).CutLength;
        var separated = PathComposition.Separate(d, new HashSet<Guid> { source.Id }); Assert.Equal(expected, ToolpathPlanner.Build(separated.Document).CutLength, 8);
        var combined = PathComposition.Combine(separated.Document, separated.Selection.ToHashSet()); Assert.Equal(expected, ToolpathPlanner.Build(combined.Document).CutLength, 8); Assert.Equal(2, Assert.Single(combined.Document.Shapes).Path!.Contours.Length);
    }
    [Fact]
    public void CombinePreservesDocumentOrderAndDoesNotBridgeOpenContours()
    {
        var a = SvgLinearPaths.Read("M10 10H20"); var b = SvgLinearPaths.Read("M30 10H40"); var untouched = new DesignShape(Guid.NewGuid(), ShapeKind.Rectangle, 50, 50, 10, 10);
        var d = Document(a, untouched, b); var result = PathComposition.Combine(d, new HashSet<Guid> { b.Id, a.Id });
        Assert.Equal(a.Id, result.Selection.Single()); Assert.Equal(untouched, result.Document.Shapes[1]);
        var combined = result.Document.Shapes[0]; Assert.Equal(2, combined.Path!.Contours.Length); Assert.All(combined.Path.Contours, c => Assert.False(c.Closed));
        var plan = ToolpathPlanner.Build(Document(combined)); Assert.Equal(20, plan.CutLength, 8); Assert.Contains(plan.Segments, s => s.Travel && s.From == new PointMm(20, 10) && s.To == new PointMm(30, 10));
    }
    [Fact]
    public void MixedLayersAndNonPathsFailWithoutChangingSource()
    {
        var a = Ring(); var b = Ring() with { Id = Guid.NewGuid(), Layer = 1 }; var d = Document(a, b); d.Layers.Add(new("Other", "#FF0000"));
        Assert.Throws<InvalidDataException>(() => PathComposition.Combine(d, new HashSet<Guid> { a.Id, b.Id })); Assert.Equal(2, d.Shapes.Count);
        var rectangle = new DesignShape(Guid.NewGuid(), ShapeKind.Rectangle, 0, 0, 10, 10); var nonpath = Document(a, rectangle);
        Assert.Throws<InvalidDataException>(() => PathComposition.Separate(nonpath, new HashSet<Guid> { a.Id, rectangle.Id }));
        Assert.Throws<InvalidDataException>(() => PathComposition.Combine(nonpath, new HashSet<Guid> { a.Id, rectangle.Id }));
    }
    [Fact]
    public void DegenerateStandaloneContourRejectsEntireSeparation()
    {
        var source = VectorPaths.Create([new([new(0, 0), new(10, 10)]), new([new(5, 5), new(5, 5)])]); var d = Document(source);
        Assert.Throws<InvalidDataException>(() => PathComposition.Separate(d, new HashSet<Guid> { source.Id })); Assert.Single(d.Shapes);
    }
    [Fact]
    public void CompositionParticipatesInHistoryAndNativeSave()
    {
        var source = Ring(); var h = new EditorHistory(); h.Reset(Document(source)); var split = PathComposition.Separate(h.Document, new HashSet<Guid> { source.Id }); h.Apply(split.Document); h.Undo(); Assert.Single(h.Document.Shapes); h.Redo(); Assert.Equal(2, h.Document.Shapes.Count);
        var file = Path.GetTempFileName(); try { ProjectFile.Save(file, h.Document); Assert.Equal(split.Selection.ToArray(), ProjectFile.Load(file).Shapes.Select(s => s.Id).ToArray()); } finally { File.Delete(file); }
    }
    [Fact]
    public void EmptyStaleAndSingleObjectSelectionsReportActionableFailures()
    {
        var a = SvgLinearPaths.Read("M10 10H20"); var d = Document(a);
        Assert.Throws<InvalidDataException>(() => PathComposition.Separate(d, new HashSet<Guid>()));
        Assert.Throws<InvalidDataException>(() => PathComposition.Combine(d, new HashSet<Guid> { Guid.NewGuid() }));
        Assert.Throws<InvalidDataException>(() => PathComposition.Combine(d, new HashSet<Guid> { a.Id }));
        Assert.Throws<InvalidDataException>(() => PathComposition.Separate(d, new HashSet<Guid> { a.Id }));
    }
}
