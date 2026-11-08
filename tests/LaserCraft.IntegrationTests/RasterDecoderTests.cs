using LaserCraft.Desktop;
using LaserCraft.Domain;
namespace LaserCraft.IntegrationTests;

public sealed class RasterDecoderTests
{
    [Fact]
    public void OriginalPngDecodesProcessesAndPlansWithoutSourceMutation()
    {
        var source = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "samples", "layer-image.png")); var before = System.Security.Cryptography.SHA256.HashData(source); var asset = RasterImages.Import(source);
        var d = LayerEditing.Add(DesignDocument.Empty(), "Photo", LayerKind.Image); d.Layers[1] = d.Layers[1] with { Interval = 1, Raster = new(RasterMode.Grayscale), Power = 35, AirAssist = true }; var shape = new DesignShape(Guid.NewGuid(), ShapeKind.Image, 20, 30, 96, 64, 1, asset); d.Shapes.Add(shape);
        var grid = RasterImages.EngravingGrid(shape, d.Layers[1]); Assert.Equal(96, grid.Width); Assert.Equal(64, grid.Height); Assert.True(grid.Darkness.Distinct().Count() > 2);
        var plan = ToolpathPlanner.Build(d, rasterDecoder: RasterImages.EngravingGrid); Assert.True(plan.CutLength > 0); Assert.All(plan.Segments.Where(s => !s.Travel), s => Assert.InRange(s.Power, 0, 35)); Assert.Equal(before, System.Security.Cryptography.SHA256.HashData(source)); Assert.Equal(source, Convert.FromBase64String(asset.DataBase64));
        var code = GrblExport.Write(plan, DeviceProfile.Simulator() with { AirAssistEnabled = true });
        Assert.Contains("M4 S0", code); Assert.Contains("M8", code); Assert.EndsWith("M5\nM9\n", code);
    }
    [Fact] public void InvalidEncodedImageIsRejected() => Assert.Throws<InvalidDataException>(() => RasterImages.Import([0, 1, 2, 3]));
}
