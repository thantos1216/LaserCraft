using LaserCraft.Domain;
using System.Collections.Immutable;

namespace LaserCraft.UnitTests;

public sealed class ArrangementTests
{
    private static DesignShape Rect(double x, double y, double w = 10, double h = 10) => new(Guid.NewGuid(), ShapeKind.Rectangle, x, y, w, h);
    private static HashSet<Guid> Ids(DesignDocument d) => d.Shapes.Select(s => s.Id).ToHashSet();
    private static DesignDocument Document(params DesignShape[] shapes) => DesignDocument.Empty() with { Shapes = [.. shapes] };
    [Fact]
    public void ArrayRepeatsTheCombinedCellInRowMajorOrderWithoutChangingOriginals()
    {
        var a = Rect(10, 20); var b = Rect(25, 25, 5, 5); var untouched = Rect(300, 300); var d = Document(a, b, untouched); var ids = new HashSet<Guid> { b.Id, a.Id };
        var measure = Arrangement.MeasureArray(d, ids, 2, 3, 2, 4); Assert.Equal(22, measure.PitchX); Assert.Equal(14, measure.PitchY); Assert.Equal(10, measure.AddedObjects);
        var result = Arrangement.RectangularArray(d, ids, 2, 3, 2, 4); Assert.Equal(13, result.Document.Shapes.Count); Assert.Equal(12, result.Selection.Count);
        Assert.Equal(d.Shapes, result.Document.Shapes.Take(3)); Assert.Same(untouched, result.Document.Shapes[2]); Assert.Equal(3, d.Shapes.Count);
        Assert.Equal((32d, 20d), (result.Document.Shapes[3].X, result.Document.Shapes[3].Y));
        Assert.Equal((10d, 34d), (result.Document.Shapes[7].X, result.Document.Shapes[7].Y));
        Assert.Equal(13, result.Document.Shapes.Select(s => s.Id).Distinct().Count()); Assert.Single(result.Document.Layers);
    }
    [Fact]
    public void ArrayCopiesImagesIntoIndependentLayersWithIdenticalSettingsAndAsset()
    {
        var asset = new RasterAsset(Guid.NewGuid(), "image/png", "AAAA", 1, 1);
        var image = new DesignShape(Guid.NewGuid(), ShapeKind.Image, 10, 10, 20, 20, 1, asset, true);
        var d = Document(Rect(0, 0), image); d.Layers.Add(new("Photo", "#123456", 800, 35, Mode: CutMode.Image, Passes: 2, AirAssist: true, Kind: LayerKind.Image, Raster: new() { Brightness = 10 }));
        var result = Arrangement.RectangularArray(d, Ids(d), 1, 3, 5, 0).Document;
        Assert.Equal(4, result.Layers.Count); Assert.Equal(3, result.Shapes.Count(s => s.Kind == ShapeKind.Image));
        foreach (var copy in result.Shapes.Where(s => s.Kind == ShapeKind.Image)) { Assert.Same(asset, copy.Image); Assert.True(copy.FlipX); Assert.Equal(d.Layers[1], result.Layers[copy.Layer] with { Name = "Photo" }); }
        Assert.Equal(2, d.Shapes.Count); Assert.Equal(2, d.Layers.Count); result.Validate();
    }
    [Theory]
    [InlineData(0, 2, 0)] [InlineData(2, 0, 0)] [InlineData(1, 1, 0)] [InlineData(100000, 100000, 0)] [InlineData(2, 2, -1)] [InlineData(2, 2, double.NaN)]
    public void InvalidArrayOptionsAreRejectedBeforeMutation(int rows, int columns, double gap)
    {
        var d = Document(Rect(0, 0)); Assert.Throws<InvalidDataException>(() => Arrangement.RectangularArray(d, Ids(d), rows, columns, gap, 0)); Assert.Single(d.Shapes);
    }
    [Fact]
    public void ArrayChecksCoordinateLayerImageAndVertexBudgetsBeforeCopying()
    {
        var offRange = Document(Rect(9999, 0)); Assert.Throws<InvalidDataException>(() => Arrangement.RectangularArray(offRange, Ids(offRange), 1, 2, 0, 0));
        var dense = VectorPaths.Create([new(Enumerable.Range(0, 100000).Select(i => new PointMm(i % 2, i / 100000d)).ToImmutableArray())]);
        var d = Document(dense); Assert.Throws<InvalidDataException>(() => Arrangement.RectangularArray(d, Ids(d), 1, 3, 1, 0));
        var asset = new RasterAsset(Guid.NewGuid(), "image/png", "AAAA", 1, 1); var images = Document(new DesignShape(Guid.NewGuid(), ShapeKind.Image, 0, 0, 1, 1, 1, asset));
        images.Layers.Add(new("Image", "#112233", Mode: CutMode.Image, Kind: LayerKind.Image));
        while (images.Layers.Count < 256) images.Layers.Add(new("Empty", "#112233")); images.Validate();
        Assert.Throws<InvalidDataException>(() => Arrangement.RectangularArray(images, Ids(images), 1, 2, 1, 0)); Assert.Equal(256, images.Layers.Count);
    }
    [Fact]
    public void ZeroExtentPathsNeedNonzeroPitchOnlyOnRepeatedAxes()
    {
        var line = VectorPaths.Create([new([new(0, 0), new(0, 10)])]); var d = Document(line);
        Assert.Throws<InvalidDataException>(() => Arrangement.MeasureArray(d, Ids(d), 1, 2, 0, 0));
        Assert.Equal(2, Arrangement.RectangularArray(d, Ids(d), 2, 1, 0, 0).Document.Shapes.Count);
    }
    [Fact]
    public void ArrayRejectsExpandedRasterBudgetsAndRotationRejectsExpandedEllipseVertices()
    {
        var image = new DesignShape(Guid.NewGuid(), ShapeKind.Image, 0, 0, 1, 1, 1, new(Guid.NewGuid(), "image/png", "AAAA", 1, 1));
        var d = Document(image); d.Layers.Add(new("Image", "#123456", Mode: CutMode.Image, Kind: LayerKind.Image));
        Assert.Throws<InvalidDataException>(() => Arrangement.RectangularArray(d, Ids(d), 1, 129, 0, 0));
        d.Shapes[0] = image with { Image = image.Image! with { DataBase64 = new string('A', 1000000) } }; d.Validate();
        Assert.Throws<InvalidDataException>(() => Arrangement.RectangularArray(d, Ids(d), 1, 25, 0, 0));
        var ellipses = DesignDocument.Empty() with { Shapes = Enumerable.Range(0, 1563).Select(_ => new DesignShape(Guid.NewGuid(), ShapeKind.Ellipse, 10, 10, 10, 10)).ToList() };
        ellipses.Validate(); Assert.Throws<InvalidDataException>(() => Arrangement.RotateVectors(ellipses, Ids(ellipses), 45));
    }
    [Theory]
    [InlineData(DistributionMode.HorizontalCenters, 45)] [InlineData(DistributionMode.HorizontalGaps, 40)]
    public void HorizontalDistributionAnchorsOuterObjectsAndPreservesOtherCoordinates(DistributionMode mode, double expectedX)
    {
        var a = Rect(0, 10, 10); var b = Rect(15, 20, 20); var c = Rect(90, 30, 30); var d = Document(c, b, a);
        var next = Arrangement.Distribute(d, Ids(d), mode); Assert.Equal(expectedX, next.Shapes[1].X); Assert.Equal(20, next.Shapes[1].Y);
        Assert.Same(c, next.Shapes[0]); Assert.Same(a, next.Shapes[2]); Assert.Equal(15, d.Shapes[1].X); Assert.Equal(d.Layers, next.Layers);
    }
    [Theory]
    [InlineData(DistributionMode.VerticalCenters, 45)] [InlineData(DistributionMode.VerticalGaps, 40)]
    public void VerticalDistributionUsesHeight(DistributionMode mode, double expectedY)
    {
        var d = Document(Rect(10, 0, 10, 10), Rect(20, 15, 10, 20), Rect(30, 90, 10, 30));
        var next = Arrangement.Distribute(d, Ids(d), mode); Assert.Equal(expectedY, next.Shapes[1].Y); Assert.Equal(20, next.Shapes[1].X);
    }
    [Fact]
    public void DistributionRejectsTooFewOrMissingObjectsAndNegativeGaps()
    {
        var d = Document(Rect(0, 0), Rect(1, 0), Rect(2, 0));
        Assert.Throws<InvalidDataException>(() => Arrangement.Distribute(d, Ids(d), DistributionMode.HorizontalGaps));
        Assert.Throws<InvalidDataException>(() => Arrangement.Distribute(d, new HashSet<Guid> { d.Shapes[0].Id }, DistributionMode.HorizontalCenters));
        var ids = Ids(d); ids.Add(Guid.NewGuid()); Assert.Throws<InvalidDataException>(() => Arrangement.Distribute(d, ids, DistributionMode.HorizontalCenters));
        Assert.Throws<InvalidDataException>(() => Arrangement.Distribute(d, Ids(d), (DistributionMode)99));
    }
    [Fact]
    public void RotationUsesCollectiveCenterAndPreservesIdentityLayerAndCutLength()
    {
        var d = Document(Rect(40, 45, 20, 10)); var next = Arrangement.RotateVectors(d, Ids(d), 90); var s = next.Shapes[0];
        Assert.Equal(d.Shapes[0].Id, s.Id); Assert.Equal(ShapeKind.Path, s.Kind); Assert.Equal(45, s.X); Assert.Equal(40, s.Y); Assert.Equal(10, s.Width); Assert.Equal(20, s.Height);
        Assert.Equal(ToolpathPlanner.Build(d).CutLength, ToolpathPlanner.Build(next).CutLength, 8); Assert.Equal(d.Layers, next.Layers);
        var restored = Arrangement.RotateVectors(next, Ids(next), -90); Assert.Equal(VectorPaths.World(VectorPaths.Convert(d.Shapes[0]))[0].Points.ToArray(), VectorPaths.World(restored.Shapes[0])[0].Points.ToArray());
    }
    [Fact]
    public void RotationConsumesMirrorTransformsAndKeepsHoleTopology()
    {
        var shape = VectorPaths.Create([new([new(20, 20), new(60, 20), new(60, 60), new(20, 60)], true), new([new(30, 30), new(40, 30), new(40, 40), new(30, 40)], true)]) with { FlipX = true };
        var d = Document(shape); var next = Arrangement.RotateVectors(d, Ids(d), 45); var s = next.Shapes[0];
        Assert.False(s.FlipX); Assert.Equal(2, s.Path!.Contours.Length); Assert.All(s.Path.Contours, c => Assert.True(c.Closed));
        Assert.Equal(ToolpathPlanner.Build(d).CutLength, ToolpathPlanner.Build(next).CutLength, 8);
    }
    [Fact]
    public void RotationKeepsSelectionSpacingRatherThanRotatingEachObjectInPlace()
    {
        var d = Document(Rect(20, 40), Rect(60, 40)); var result = Arrangement.RotateVectors(d, Ids(d), 90);
        Assert.Equal(result.Shapes[0].X, result.Shapes[1].X); Assert.Equal(40, result.Shapes[1].Y - result.Shapes[0].Y, 8);
        var ellipse = Document(new DesignShape(Guid.NewGuid(), ShapeKind.Ellipse, 10, 10, 10, 20));
        Assert.Equal(128, Arrangement.RotateVectors(ellipse, Ids(ellipse), 90).Shapes[0].Path!.Contours[0].Points.Length);
    }
    [Fact]
    public void RotationAndReversalRejectRasterOrNonPathSelectionsAtomically()
    {
        var d = Document(new DesignShape(Guid.NewGuid(), ShapeKind.Image, 0, 0, 10, 10, 1, new(Guid.NewGuid(), "image/png", "AAAA", 1, 1)), Rect(20, 20));
        d.Layers.Add(new("Image", "#123456", Mode: CutMode.Image, Kind: LayerKind.Image)); d.Validate();
        Assert.Throws<InvalidDataException>(() => Arrangement.RotateVectors(d, Ids(d), 90));
        Assert.Throws<InvalidDataException>(() => Arrangement.ReversePaths(d, Ids(d))); Assert.Equal(ShapeKind.Rectangle, d.Shapes[1].Kind);
    }
    [Theory]
    [InlineData(0)] [InlineData(360)] [InlineData(-360)] [InlineData(361)] [InlineData(double.NaN)]
    public void InvalidRotationDoesNotChangeTheDocument(double angle)
    {
        var d = Document(Rect(10, 10)); Assert.Throws<InvalidDataException>(() => Arrangement.RotateVectors(d, Ids(d), angle)); Assert.Equal(ShapeKind.Rectangle, d.Shapes[0].Kind);
    }
    [Fact]
    public void ReversePreservesClosedStartAndReversesOpenEndpointsIncludingMirrors()
    {
        var s = VectorPaths.Create([new([new(10, 10), new(30, 10), new(30, 30)], true), new([new(15, 15), new(25, 20)])]) with { FlipX = true };
        var d = Document(s); var next = Arrangement.ReversePaths(d, Ids(d)); var before = VectorPaths.World(s); var after = VectorPaths.World(next.Shapes[0]);
        Assert.Equal(before[0].Points[0], after[0].Points[0]); Assert.Equal(before[0].Points[1], after[0].Points[^1]); Assert.Equal(before[1].Points[0], after[1].Points[^1]);
        var restored = Arrangement.ReversePaths(next, Ids(next)).Shapes[0]; Assert.Equal(s.Id, restored.Id); Assert.Equal(s.FlipX, restored.FlipX);
        for (int i = 0; i < before.Length; i++) Assert.Equal(before[i].Points.ToArray(), VectorPaths.World(restored)[i].Points.ToArray());
    }
}
