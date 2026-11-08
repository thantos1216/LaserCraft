using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class ImageTracingTests
{
    private static DesignShape Frame(int width, int height) => new(Guid.NewGuid(), ShapeKind.Image, 10, 20, width, height, Image: new(Guid.NewGuid(), "image/png", "AAAA", width, height));
    private static RasterGrid Mask(params string[] rows) => new(rows[0].Length, rows.Length, rows.SelectMany(row => row.Select(c => c == '#' ? (byte)255 : (byte)0)).ToArray());
    private static double Area(DesignShape shape) => Math.Abs(VectorPaths.World(shape).Sum(c => Enumerable.Range(0, c.Points.Length).Sum(i => c.Points[i].X * c.Points[(i + 1) % c.Points.Length].Y - c.Points[(i + 1) % c.Points.Length].X * c.Points[i].Y) / 2));
    [Fact]
    public void RectangleBoundaryCollapsesCollinearPixelsToFourCorners()
    {
        var source = Frame(4, 2); var result = ImageTracing.Trace(Mask("####", "####"), source, 1);
        var contour = Assert.Single(result.Path!.Path!.Contours); Assert.Equal(4, contour.Points.Length); Assert.True(contour.Closed); Assert.Equal(8, Area(result.Path), 8);
        Assert.Equal(12, VectorPaths.Edges(result.Path).Sum(e => VectorPaths.Distance(e.From, e.To, e.To)), 8);
    }
    [Fact]
    public void RingRetainsHoleForCuttingAndFill()
    {
        var result = ImageTracing.Trace(Mask("###", "#.#", "###"), Frame(3, 3), 1); Assert.Equal(2, result.Path!.Path!.Contours.Length); Assert.Equal(8, Area(result.Path), 8);
        var d = DesignDocument.Empty() with { Shapes = [result.Path] }; Assert.Equal(16, ToolpathPlanner.Build(d).CutLength, 8);
        d.Layers[0] = d.Layers[0] with { Mode = CutMode.Fill, Interval = 1 }; Assert.Equal(8, ToolpathPlanner.Build(d).CutLength, 8); Assert.False(VectorPaths.Contains(result.Path, new(11.5, 21.5)));
    }
    [Fact]
    public void DiagonalContactsStaySeparateAndSpecklesUseFourConnectivity()
    {
        var mask = Mask("#.", ".#"); var source = Frame(2, 2);
        Assert.Equal(2, ImageTracing.Trace(mask, source, 1).Path!.Path!.Contours.Length);
        var removed = ImageTracing.Trace(mask, source, 2); Assert.Null(removed.Path); Assert.Equal(2, removed.RemovedComponents);
        var kept = ImageTracing.Trace(Mask("##..", "..#.", "...#"), Frame(4, 3), 2); Assert.Equal(2, kept.RetainedPixels); Assert.Equal(2, kept.RemovedComponents); Assert.Equal(2, Area(kept.Path!), 8);
    }
    [Fact]
    public void ExhaustiveThreeByThreeMasksPreserveAreaAndEveryPixelCenter()
    {
        var source = Frame(3, 3);
        for (int bits = 0; bits < 512; bits++)
        {
            var cells = Enumerable.Range(0, 9).Select(i => (bits & (1 << i)) != 0 ? (byte)255 : (byte)0).ToArray();
            var result = ImageTracing.Trace(new(3, 3, cells), source, 1);
            if (bits == 0) { Assert.Null(result.Path); continue; }
            Assert.Equal(cells.Count(v => v != 0), Area(result.Path!), 8);
            for (int i = 0; i < cells.Length; i++) Assert.Equal(cells[i] != 0, VectorPaths.Contains(result.Path!, new(10 + i % 3 + .5, 20 + i / 3 + .5)));
        }
    }
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ImageMirrorsMapContoursToTheSamePhysicalArtwork(bool flipX, bool flipY)
    {
        var source = Frame(3, 2) with { Width = 30, Height = 20, FlipX = flipX, FlipY = flipY }; var result = ImageTracing.Trace(Mask("#..", "..."), source, 1);
        Assert.Equal(flipX ? 30 : 10, result.Path!.X, 8); Assert.Equal(flipY ? 30 : 20, result.Path.Y, 8); Assert.Equal(100, Area(result.Path), 8);
    }
    [Fact]
    public void InputMaskIsNotMutatedAndCancellationStopsProcessing()
    {
        var mask = Mask("#.#", "###"); var copy = mask.Darkness.ToArray(); ImageTracing.Trace(mask, Frame(3, 2), 2); Assert.Equal(copy, mask.Darkness);
        using var c = new CancellationTokenSource(); c.Cancel(); Assert.Throws<OperationCanceledException>(() => ImageTracing.Trace(mask, Frame(3, 2), cancellationToken: c.Token));
    }
    [Fact]
    public void EmptyMasksCannotCreateAnOutputLayer()
    {
        var d = ImageDocument(); var source = d.Shapes[0]; var result = ImageTracing.Trace(Mask("..", ".."), source); Assert.Null(result.Path);
        Assert.Throws<InvalidDataException>(() => ImageTracing.Apply(d, source.Id, result)); Assert.Equal(2, d.Layers.Count);
    }
    [Fact]
    public void ApplyCreatesSeparateVectorLayerDisablesSourceAndIsUndoable()
    {
        var d = ImageDocument(); var source = d.Shapes[0]; var result = ImageTracing.Trace(Mask("##", "##"), source, 1); var next = ImageTracing.Apply(d, source.Id, result);
        Assert.Equal(3, next.Layers.Count); Assert.Equal(2, next.Shapes.Count); Assert.False(next.Layers[1].Output); Assert.True(d.Layers[1].Output); Assert.Equal(LayerKind.Vector, next.Layers[2].Kind); Assert.Equal(2, next.Shapes[^1].Layer);
        var history = new EditorHistory(); history.Reset(d); history.Apply(next); history.Undo(); Assert.Single(history.Document.Shapes); Assert.True(history.Document.Layers[1].Output); history.Redo(); Assert.Equal(2, history.Document.Shapes.Count);
        var removed = ImageTracing.Apply(d, source.Id, result, true); Assert.Single(removed.Shapes); Assert.Equal(ShapeKind.Path, removed.Shapes[0].Kind); Assert.Single(d.Shapes);
    }
    [Fact]
    public void MovedOrReplacedSourceRejectsStaleTrace()
    {
        var d = ImageDocument(); var source = d.Shapes[0]; var result = ImageTracing.Trace(Mask("##", "##"), source, 1);
        var changed = d with { Shapes = [source with { X = 15 }] }; Assert.Throws<InvalidDataException>(() => ImageTracing.Apply(changed, source.Id, result));
        Assert.Throws<InvalidDataException>(() => ImageTracing.Apply(d, Guid.NewGuid(), result));
    }
    [Fact]
    public void ExcessiveWorkAndInvalidParametersFailExplicitly()
    {
        Assert.Throws<InvalidDataException>(() => ImageTracing.Trace(Mask("##"), Frame(2, 1), 0));
        Assert.Throws<InvalidDataException>(() => ImageTracing.Trace(new(1025, 1, new byte[1025]), Frame(1025, 1)));
        var dense = Enumerable.Range(0, 512 * 512).Select(i => (i % 512 + i / 512) % 2 == 0 ? (byte)255 : (byte)0).ToArray();
        Assert.Throws<InvalidDataException>(() => ImageTracing.Trace(new(512, 512, dense), Frame(512, 512), 1));
    }
    private static DesignDocument ImageDocument()
    {
        var d = LayerEditing.Add(DesignDocument.Empty(), "Artwork", LayerKind.Image); d.Shapes.Add(Frame(2, 2) with { Layer = 1 }); return d;
    }
}
