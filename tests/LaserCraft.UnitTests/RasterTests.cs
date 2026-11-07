using LaserCraft.Domain;
namespace LaserCraft.UnitTests;

public sealed class RasterTests
{
    private static byte[] Rgba(params byte[] gray) => gray.SelectMany(v => new[] { v, v, v, (byte)255 }).ToArray();
    private static DesignDocument ImageDocument(RasterSettings? settings = null, double width = 2, double height = 2)
    {
        var d = LayerEditing.Add(DesignDocument.Empty(), "Image", LayerKind.Image); d.Layers[1] = d.Layers[1] with { Interval = 1, Power = 40, AirAssist = true, Raster = settings };
        d.Shapes.Add(new(Guid.NewGuid(), ShapeKind.Image, 10, 20, width, height, 1, new(Guid.NewGuid(), "image/png", "AAAA", 2, 2))); return d;
    }
    [Fact] public void ThresholdMapsBlackToFullPowerAndWhiteToNoPower() => Assert.Equal(new byte[] { 255, 0 }, RasterProcessing.ProcessRgba(2, 1, Rgba(0, 255), new(RasterMode.Threshold)).Darkness);
    [Theory]
    [InlineData(RasterMode.Threshold)]
    [InlineData(RasterMode.FloydSteinberg)]
    [InlineData(RasterMode.OrderedBayer)]
    [InlineData(RasterMode.Grayscale)]
    public void TransparentPixelsStayOffEvenWhenInverted(RasterMode mode)
    {
        var grid = RasterProcessing.ProcessRgba(2, 1, [0, 0, 0, 0, 255, 255, 255, 255], new(mode, Invert: true)); Assert.Equal(new byte[] { 0, 255 }, grid.Darkness);
    }
    [Fact] public void FloydSteinbergHasDeterministicErrorDiffusion() => Assert.Equal(new byte[] { 255, 0, 255, 0 }, RasterProcessing.ProcessRgba(4, 1, Rgba(127, 127, 127, 127), new(RasterMode.FloydSteinberg)).Darkness);
    [Fact]
    public void BayerHalfToneHasHalfOfItsCellsEnabled()
    {
        var grid = RasterProcessing.ProcessRgba(4, 4, Rgba(Enumerable.Repeat((byte)128, 16).ToArray()), new(RasterMode.OrderedBayer)); Assert.Equal(8, grid.Darkness.Count(v => v == 255));
    }
    [Fact]
    public void GammaBrightensIntermediateTonesWithoutMutatingInput()
    {
        var rgba = Rgba(128); var copy = rgba.ToArray(); var normal = RasterProcessing.ProcessRgba(1, 1, rgba, new(RasterMode.Grayscale)); var adjusted = RasterProcessing.ProcessRgba(1, 1, rgba, new(RasterMode.Grayscale, Gamma: 2)); Assert.True(adjusted.Darkness[0] < normal.Darkness[0]); Assert.Equal(copy, rgba);
    }
    [Fact]
    public void InvalidAndCancelledProcessingIsRejected()
    {
        Assert.Throws<InvalidDataException>(() => RasterProcessing.ProcessRgba(2, 2, [1, 2, 3], new())); using var cancel = new CancellationTokenSource(); cancel.Cancel(); Assert.Throws<OperationCanceledException>(() => RasterProcessing.ProcessRgba(1, 1, Rgba(128), new(), cancel.Token)); Assert.Throws<InvalidDataException>(() => new RasterSettings(Gamma: 0).Validate());
    }
    [Fact]
    public void RasterRowsCarryPowerAndScanInBothDirections()
    {
        var d = ImageDocument(); var plan = ToolpathPlanner.Build(d, rasterDecoder: (_, _, _) => new(2, 2, [255, 0, 0, 128])); var cuts = plan.Segments.Where(s => !s.Travel).ToArray();
        Assert.Equal(2, cuts.Length); Assert.Equal(new PointMm(10, 20.5), cuts[0].From); Assert.Equal(new PointMm(11, 20.5), cuts[0].To); Assert.Equal(40, cuts[0].Power); Assert.Equal(new PointMm(12, 21.5), cuts[1].From); Assert.Equal(new PointMm(11, 21.5), cuts[1].To); Assert.Equal(40 * 128 / 255.0, cuts[1].Power, 8); Assert.All(cuts, s => Assert.True(s.AirAssist)); Assert.All(plan.Segments.Where(s => s.Travel), s => { Assert.Equal(0, s.Power); Assert.False(s.AirAssist); });
    }
    [Fact]
    public void HorizontalAndVerticalMirrorsMapRasterSamples()
    {
        var d = ImageDocument(); d.Shapes[0] = d.Shapes[0] with { FlipX = true, FlipY = true }; var plan = ToolpathPlanner.Build(d, rasterDecoder: (_, _, _) => new(2, 2, [255, 0, 0, 0])); var cut = Assert.Single(plan.Segments, s => !s.Travel); Assert.Equal(new PointMm(12, 21.5), cut.From); Assert.Equal(new PointMm(11, 21.5), cut.To);
    }
    [Fact]
    public void UnidirectionalRowsAndRepeatedPassesArePreserved()
    {
        var d = ImageDocument(new(Bidirectional: false)); d.Layers[1] = d.Layers[1] with { Passes = 2 }; var p = ToolpathPlanner.Build(d, rasterDecoder: (_, _, _) => new(2, 2, [255, 255, 255, 255])); var cuts = p.Segments.Where(s => !s.Travel).ToArray(); Assert.Equal(4, cuts.Length); Assert.All(cuts, s => Assert.True(s.To.X > s.From.X));
    }
    [Fact]
    public void OverscanIsLaserOffAndBedChecked()
    {
        var d = ImageDocument(new(Overscan: 2), 2, 1); var p = ToolpathPlanner.Build(d, rasterDecoder: (_, _, _) => new(2, 1, [255, 255])); Assert.Equal(8, p.Segments[0].To.X); Assert.Equal(14, p.Segments[^1].To.X); Assert.True(p.Segments[^1].Travel); d.Shapes[0] = d.Shapes[0] with { X = 1 }; Assert.Throws<InvalidDataException>(() => ToolpathPlanner.Build(d, rasterDecoder: (_, _, _) => new(2, 1, [255, 255])));
    }
    [Fact]
    public void WhiteRasterSkipsAllRowsAndDoesNotAddCutLength()
    {
        var p = ToolpathPlanner.Build(ImageDocument(), rasterDecoder: (_, _, _) => new(2, 2, [0, 0, 0, 0])); Assert.Empty(p.Segments); Assert.Equal(0, p.CutLength);
    }
    [Fact]
    public void ImageSettingsRoundTripWithLayers()
    {
        var d = ImageDocument(new(RasterMode.OrderedBayer, 12, 20, 1.5, true, 140, false, 2)); var json = System.Text.Json.JsonSerializer.Serialize(d); var loaded = System.Text.Json.JsonSerializer.Deserialize<DesignDocument>(json)!; loaded.Validate(); Assert.Equal(d.Layers[1].Raster, loaded.Layers[1].Raster);
    }
    [Fact]
    public void ExcessiveRasterResolutionIsRejectedBeforeDecoding()
    {
        var d = ImageDocument(width: 100, height: 100); d.Layers[1] = d.Layers[1] with { Interval = .01 }; bool decoded = false;
        Assert.Throws<InvalidDataException>(() => ToolpathPlanner.Build(d, rasterDecoder: (_, _, _) => { decoded = true; return new(1, 1, [255]); })); Assert.False(decoded);
    }
}
