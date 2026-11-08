using LaserCraft.Desktop;
using LaserCraft.Domain;
using Xunit.Abstractions;

namespace LaserCraft.IntegrationTests;

public sealed class PhotoTracingTests(ITestOutputHelper output)
{
    [Fact]
    public void NasaPhotographSupportsTonalAndSketchTracingWithoutChangingSource()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "nasa-blue-marble.jpg")); var asset = RasterImages.Import(bytes);
        var source = new DesignShape(Guid.NewGuid(), ShapeKind.Image, 100, 100, 200, 200, 1, asset); var layer = new CutLayer("Photo", "#D946EF", Mode: CutMode.Image, Kind: LayerKind.Image, Interval: 200.0 / 512, Raster: new(RasterMode.Grayscale));
        var gray = RasterImages.EngravingGrid(source, layer); var clock = System.Diagnostics.Stopwatch.StartNew();
        var traced = PhotoTracing.Build(gray, source, new(Cutoff: 20, Threshold: 160, NoiseSmoothing: .8, Smoothness: .5, Optimize: .7), 8);
        var after = traced.Trace.Path!.Path!.Contours.Sum(c => c.Points.Length); output.WriteLine($"Photo: {traced.Trace.Path.Path.Contours.Length} contours, {traced.OriginalVertices} -> {after} vertices, {clock.ElapsedMilliseconds} ms. {traced.RefinementNote}");
        Assert.True(traced.Trace.Path.Path.Contours.Length > 5); Assert.True(after < traced.OriginalVertices); Assert.StartsWith("Refined", traced.RefinementNote);
        var sketch = PhotoTracing.Build(gray, source, new(NoiseSmoothing: .8, Sketch: true, LocalContrast: 8), 8); Assert.NotNull(sketch.Trace.Path); output.WriteLine($"Sketch: {sketch.Trace.Path.Path!.Contours.Length} contours");
        Assert.Equal(bytes, Convert.FromBase64String(asset.DataBase64));
        var d = LayerEditing.Add(DesignDocument.Empty(), "Photo", LayerKind.Image); d.Shapes.Add(source); var applied = ImageTracing.Apply(d, source.Id, traced.Trace); var plan = ToolpathPlanner.Build(applied); Assert.True(plan.CutLength > 0); Assert.All(plan.Segments, s => Assert.Equal(2, s.Layer));
    }
}
