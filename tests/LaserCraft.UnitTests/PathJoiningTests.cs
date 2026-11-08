using LaserCraft.Domain;
using System.Collections.Immutable;

namespace LaserCraft.UnitTests;

public sealed class PathJoiningTests
{
    private static DesignShape Line(params PointMm[] points) => VectorPaths.Create([new(points.ToImmutableArray())]);
    private static DesignDocument Document(params DesignShape[] shapes) { var d = DesignDocument.Empty(); d.Shapes.AddRange(shapes); return d; }
    private static PathJoinResult Join(DesignDocument d, double max = 1, PathJoinMode mode = PathJoinMode.MergeAtMidpoint) => PathJoining.Prepare(d, d.Shapes.Select(s => s.Id).ToHashSet(), max, mode);
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void AllEndpointOrientationsJoinIntoTheSameContinuousGeometry(bool reverseA, bool reverseB)
    {
        var a = new PointMm[] { new(10, 10), new(20, 10) }; var b = new PointMm[] { new(20, 10), new(30, 20) };
        var result = Join(Document(Line(reverseA ? a.Reverse().ToArray() : a), Line(reverseB ? b.Reverse().ToArray() : b)), 0);
        Assert.Equal(new PointMm[] { new(10, 10), new(20, 10), new(30, 20) }, VectorPaths.World(Assert.Single(result.Document.Shapes))[0].Points.ToArray()); Assert.Equal(0, result.Gap);
    }
    [Fact]
    public void MergeUsesMidpointAndBridgeRetainsBothOriginalEndpoints()
    {
        var d = Document(Line(new(10, 10), new(20, 10)), Line(new(22, 10), new(30, 20)));
        var merged = Join(d, 2); var bridged = Join(d, 2, PathJoinMode.BridgeGap);
        Assert.Equal(new PointMm(21, 10), VectorPaths.World(merged.Document.Shapes[0])[0].Points[1]);
        var points = VectorPaths.World(bridged.Document.Shapes[0])[0].Points; Assert.Equal(new PointMm(20, 10), points[1]); Assert.Equal(new PointMm(22, 10), points[2]);
        Assert.Equal(ToolpathPlanner.Build(d).CutLength + 2, ToolpathPlanner.Build(bridged.Document).CutLength, 8);
        Assert.Contains(ToolpathPlanner.Build(bridged.Document).Segments, s => !s.Travel && s.From == new PointMm(20, 10) && s.To == new PointMm(22, 10) && s.Power == 20);
        Assert.Equal(2, d.Shapes.Count); Assert.Equal(new PointMm(20, 10), VectorPaths.World(d.Shapes[0])[0].Points[^1]);
    }
    [Fact]
    public void ExactGapBoundarySucceedsAndSmallerLimitRejectsAtomically()
    {
        var d = Document(Line(new(0, 0), new(10, 0)), Line(new(13, 4), new(20, 10))); Assert.Equal(5, Join(d, 5).Gap);
        Assert.Throws<InvalidDataException>(() => Join(d, 4.999)); Assert.Equal(2, d.Shapes.Count);
    }
    [Fact]
    public void TwoOpenContoursInOneObjectCanBeJoinedAndRemainOpen()
    {
        var a = Line(new(10, 10), new(20, 10)); var b = Line(new(20, 10), new(10, 10));
        var source = VectorPaths.Create(VectorPaths.World(a).AddRange(VectorPaths.World(b))); var joined = Join(Document(source), 0);
        var shape = Assert.Single(joined.Document.Shapes); Assert.Equal(source.Id, shape.Id); Assert.Single(shape.Path!.Contours); Assert.False(shape.Path.Contours[0].Closed);
        var d = joined.Document; d.Layers[0] = d.Layers[0] with { Mode = CutMode.Fill }; Assert.Throws<InvalidDataException>(() => ToolpathPlanner.Build(d));
    }
    [Fact]
    public void LayerIdentityAndUnaffectedDocumentOrderArePreserved()
    {
        var a = Line(new(10, 10), new(20, 10)) with { FlipX = true }; var b = Line(new(20, 10), new(30, 20)); var other = new DesignShape(Guid.NewGuid(), ShapeKind.Rectangle, 50, 50, 5, 5);
        var d = Document(a, other, b); var joined = PathJoining.Prepare(d, new HashSet<Guid> { b.Id, a.Id }, 0, PathJoinMode.BridgeGap);
        Assert.Equal(a.Id, joined.JoinedId); Assert.Equal(a.Layer, joined.Document.Shapes[0].Layer); Assert.Equal(other, joined.Document.Shapes[1]);
        Assert.Equal(new PointMm(10, 10), VectorPaths.World(joined.Document.Shapes[0])[0].Points[0]);
    }
    [Fact]
    public void InvalidSelectionsModesAndGapValuesAreRejected()
    {
        var a = Line(new(0, 0), new(10, 0)); var b = Line(new(10, 0), new(20, 0)); var d = Document(a, b);
        foreach (var gap in new[] { -1, double.NaN, double.PositiveInfinity, 20001 }) Assert.Throws<InvalidDataException>(() => Join(d, gap));
        Assert.Throws<InvalidDataException>(() => Join(d, 1, (PathJoinMode)999));
        Assert.Throws<InvalidDataException>(() => PathJoining.Prepare(d, new HashSet<Guid> { Guid.NewGuid() }, 1, PathJoinMode.MergeAtMidpoint));
        Assert.Throws<InvalidDataException>(() => Join(Document(a)));
        var closed = VectorPaths.Convert(new DesignShape(Guid.NewGuid(), ShapeKind.Rectangle, 10, 10, 5, 5)); Assert.Throws<InvalidDataException>(() => Join(Document(a, closed)));
        d.Layers.Add(new("Other", "#FF0000")); d.Shapes[1] = b with { Layer = 1 }; Assert.Throws<InvalidDataException>(() => Join(d));
    }
    [Fact]
    public void JoiningRespectsThePerObjectVertexBudget()
    {
        var a = Line(Enumerable.Range(0, 60000).Select(i => new PointMm(i / 1000.0, 10)).ToArray());
        var b = Line(Enumerable.Range(0, 60000).Select(i => new PointMm(60 + i / 1000.0, 10)).ToArray()); var d = Document(a, b);
        Assert.Throws<InvalidDataException>(() => Join(d)); Assert.Equal(2, d.Shapes.Count);
    }
    [Fact]
    public void JoinedGeometryRoundTripsAndUndoRestoresBothSources()
    {
        var d = Document(Line(new(10, 10), new(20, 10)), Line(new(20, 10), new(30, 20))); var history = new EditorHistory(); history.Reset(d); var joined = Join(d, 0); history.Apply(joined.Document); history.Undo(); Assert.Equal(2, history.Document.Shapes.Count); history.Redo(); Assert.Single(history.Document.Shapes);
        var file = Path.GetTempFileName();
        try { ProjectFile.Save(file, history.Document); Assert.Equal(ToolpathPlanner.Build(history.Document).CutLength, ToolpathPlanner.Build(ProjectFile.Load(file)).CutLength, 8); }
        finally { File.Delete(file); }
    }
}
