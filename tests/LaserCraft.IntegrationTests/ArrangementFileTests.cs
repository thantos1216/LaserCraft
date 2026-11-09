using LaserCraft.Domain;
using LaserCraft.Desktop;

namespace LaserCraft.IntegrationTests;

public sealed class ArrangementFileTests
{
    [Fact]
    public void ImageArrayCopiesDecodeAndEngraveOnIndependentOrderedLayers()
    {
        var bytes = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "layer-image.png")); var asset = RasterImages.Import(bytes);
        var d = LayerEditing.Add(DesignDocument.Empty(), "Photo", LayerKind.Image); d.Layers[1] = d.Layers[1] with { Interval = 1, Power = 35, Raster = new(RasterMode.Grayscale), AirAssist = true };
        var shape = new DesignShape(Guid.NewGuid(), ShapeKind.Image, 10, 10, 12, 8, 1, asset, FlipX: true); d.Shapes.Add(shape);
        var result = Arrangement.RectangularArray(d, new HashSet<Guid> { shape.Id }, 1, 2, 3, 0).Document;
        var originalPixels = RasterImages.EngravingGrid(shape, d.Layers[1]).Darkness;
        foreach (var s in result.Shapes) Assert.Equal(originalPixels, RasterImages.EngravingGrid(s, result.Layers[s.Layer]).Darkness);
        var plan = ToolpathPlanner.Build(result, rasterDecoder: RasterImages.EngravingGrid);
        Assert.Equal(new[] { 1, 2 }, plan.Segments.Where(s => !s.Travel).Select(s => s.Layer).Distinct());
        Assert.All(plan.Segments.Where(s => !s.Travel), s => Assert.InRange(s.Power, 0, 35));
        Assert.Contains("M8\n", GrblExport.Write(plan, DeviceProfile.Simulator() with { AirAssistEnabled = true }));
        Assert.Equal(bytes, Convert.FromBase64String(result.Shapes[1].Image!.DataBase64));
    }
    [Fact]
    public void ArrangementEditingRoundTripsThroughHistoryFilesAndOutput()
    {
        var original = ProjectFile.Load(Path.Combine(AppContext.BaseDirectory, "samples", "arrangement-demo.lcraft"));
        var ids = original.Shapes.Select(s => s.Id).ToHashSet(); var history = new EditorHistory(); history.Reset(original);
        var distributed = Arrangement.Distribute(original, ids, DistributionMode.HorizontalGaps); Assert.Equal(60, distributed.Shapes[1].X);
        history.Apply(distributed); history.Undo(); Assert.Equal(55, history.Document.Shapes[1].X); history.Redo();
        var rotated = Arrangement.RotateVectors(history.Document, ids, 90); Assert.All(rotated.Shapes, s => Assert.Equal(ShapeKind.Path, s.Kind));
        var reversed = Arrangement.ReversePaths(rotated, ids); Assert.Equal(ToolpathPlanner.Build(rotated).CutLength, ToolpathPlanner.Build(reversed).CutLength, 8);
        var array = Arrangement.RectangularArray(reversed, ids, 2, 2, 5, 5); history.Apply(array.Document); Assert.Equal(12, history.Document.Shapes.Count);
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".lcraft");
        try
        {
            ProjectFile.Save(path, array.Document); var loaded = ProjectFile.Load(path); Assert.Equal(array.Selection.Order(), loaded.Shapes.Select(s => s.Id).Order());
            var plan = ToolpathPlanner.Build(loaded); Assert.Equal(ToolpathPlanner.Build(original).CutLength * 4, plan.CutLength, 8);
            var exported = GrblExport.Write(plan, DeviceProfile.Simulator()); Assert.EndsWith("M5\nM9\n", exported);
            var svg = SvgImport.Read(SvgExport.Write(loaded)); Assert.Equal(12, svg.Count); Assert.All(svg, s => Assert.Equal(ShapeKind.Path, s.Kind));
            history.Undo(); Assert.Equal(3, history.Document.Shapes.Count); history.Redo(); Assert.Equal(12, history.Document.Shapes.Count);
        }
        finally { File.Delete(path); }
    }
}
