using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class PhotoTracingTests
{
    private static RasterGrid Gray(int w, int h, Func<int, int, int> value) => new(w, h, Enumerable.Range(0, w * h).Select(i => (byte)(255 - value(i % w, i / w))).ToArray());
    private static DesignShape Frame(int w, int h) => new(Guid.NewGuid(), ShapeKind.Image, 10, 20, w, h);
    [Fact]
    public void BrightnessRangeExcludesDarkBackgroundAndRetainsMidtones()
    {
        var tones = new[] { 0, 50, 127, 128, 200, 255 }; var gray = Gray(6, 1, (x, y) => tones[x]);
        var result = PhotoTracing.Prepare(gray, new(Cutoff: 50, Threshold: 128)); Assert.Equal(new byte[] { 0, 255, 255, 0, 0, 0 }, result.Mask.Darkness);
        Assert.Equal(gray.Darkness, result.Grayscale.Darkness);
    }
    [Fact]
    public void NoiseSmoothingRemovesIsolatedDarkNoiseWithoutMutatingInput()
    {
        var gray = Gray(9, 9, (x, y) => x == 4 && y == 4 ? 0 : 255); var saved = gray.Darkness.ToArray();
        Assert.Equal(255, PhotoTracing.Prepare(gray, new()).Mask.Darkness[40]); Assert.Equal(0, PhotoTracing.Prepare(gray, new(NoiseSmoothing: 1)).Mask.Darkness[40]); Assert.Equal(saved, gray.Darkness);
    }
    [Fact]
    public void LocalContrastFindsInkUnderUnevenIllumination()
    {
        var gray = Gray(64, 32, (x, y) => 100 + x * 2 - (y == 16 ? 40 : 0));
        var global = PhotoTracing.Prepare(gray, new(Threshold: 100)); var sketch = PhotoTracing.Prepare(gray, new(Sketch: true, LocalContrast: 8));
        Assert.Equal(0, global.Mask.Darkness[16 * 64 + 50]); Assert.Equal(255, sketch.Mask.Darkness[16 * 64 + 50]); Assert.Equal(0, sketch.Mask.Darkness[8 * 64 + 50]);
    }
    [Fact]
    public void OptimizeReducesStaircaseNodesAndKeepsClosedContours()
    {
        var gray = Gray(20, 20, (x, y) => x <= y ? 0 : 255); var raw = PhotoTracing.Build(gray, Frame(20, 20), new(), 1); var optimized = PhotoTracing.Build(gray, raw.Trace.Source, new(Optimize: .8), 1);
        Assert.StartsWith("Refined", optimized.RefinementNote); Assert.True(optimized.Trace.Path!.Path!.Contours.Sum(c => c.Points.Length) < raw.OriginalVertices); Assert.All(optimized.Trace.Path.Path.Contours, c => Assert.True(c.Closed));
    }
    [Fact]
    public void SmoothingUsesLocalRadiusAndPreservesHoleTopology()
    {
        var gray = Gray(24, 24, (x, y) => x >= 2 && x < 22 && y >= 2 && y < 22 && !(x >= 8 && x < 16 && y >= 8 && y < 16) ? 0 : 255); var result = PhotoTracing.Build(gray, Frame(24, 24), new(Smoothness: .8, Optimize: .2), 1);
        Assert.StartsWith("Refined", result.RefinementNote); Assert.Equal(2, result.Trace.Path!.Path!.Contours.Length); Assert.False(VectorPaths.Contains(result.Trace.Path, new(22, 32))); Assert.True(VectorPaths.Contains(result.Trace.Path, new(14, 24)));
        Assert.InRange(result.Trace.Path.X, 12, 13); Assert.InRange(result.Trace.Path.Width, 18, 20);
    }
    [Fact]
    public void ExtremeOptimizationCannotCollapseSmallRegions()
    {
        var gray = Gray(8, 8, (x, y) => x == 2 && y == 2 ? 0 : 255); var result = PhotoTracing.Build(gray, Frame(8, 8), new(Optimize: 2), 1);
        Assert.NotNull(result.Trace.Path); Assert.True(VectorPaths.Contains(result.Trace.Path, new(12.5, 22.5)));
    }
    [Fact]
    public void InvalidTuningAndCancelledWorkAreRejected()
    {
        var gray = Gray(8, 8, (x, y) => 128); Assert.Throws<InvalidDataException>(() => PhotoTracing.Prepare(gray, new(Cutoff: 200, Threshold: 100))); Assert.Throws<InvalidDataException>(() => new TraceTuning(Optimize: double.NaN).Validate());
        using var c = new CancellationTokenSource(); c.Cancel(); Assert.Throws<OperationCanceledException>(() => PhotoTracing.Build(gray, Frame(8, 8), new(NoiseSmoothing: 2), cancellationToken: c.Token));
    }
    [Fact]
    public void LargerPhotoSampleFramesAreSupportedWithinTheWorkBudget()
    {
        var gray = Gray(1024, 512, (x, y) => x > 100 && x < 900 && y > 100 && y < 400 ? 0 : 255); var result = PhotoTracing.Build(gray, Frame(1024, 512), new(Optimize: .5), 1); Assert.NotNull(result.Trace.Path); Assert.Single(result.Trace.Path.Path!.Contours);
    }
    [Fact]
    public void RefiningEveryThreeByThreePatternRetainsItsContourCountAndWinding()
    {
        var frame = Frame(3, 3);
        for (int bits = 1; bits < 512; bits++)
        {
            var gray = Gray(3, 3, (x, y) => (bits & (1 << (y * 3 + x))) != 0 ? 0 : 255); var raw = PhotoTracing.Build(gray, frame, new(), 1); var refined = PhotoTracing.Build(gray, frame, new(Smoothness: .7, Optimize: .9), 1);
            var a = VectorPaths.World(raw.Trace.Path!); var b = VectorPaths.World(refined.Trace.Path!); Assert.Equal(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++) { double Area(PathContour c) => Enumerable.Range(0, c.Points.Length).Sum(j => c.Points[j].X * c.Points[(j + 1) % c.Points.Length].Y - c.Points[(j + 1) % c.Points.Length].X * c.Points[j].Y); Assert.Equal(Math.Sign(Area(a[i])), Math.Sign(Area(b[i]))); }
        }
    }
}
