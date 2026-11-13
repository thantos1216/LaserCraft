using LaserCraft.Domain;
using LaserCraft.Desktop;
namespace LaserCraft.IntegrationTests;
public sealed class ImagePreparationTests
{
    private static DesignDocument Sample()=>ProjectFile.Load(Path.Combine(AppContext.BaseDirectory,"samples","vector-and-image-demo.lcraft"));
    [Theory][InlineData(RasterMode.Grayscale)][InlineData(RasterMode.Stucki)][InlineData(RasterMode.Threshold)]
    public void PreparedSettingsUseOutputResolutionAndApplyWithoutChangingArtwork(RasterMode mode)
    {
        var d=Sample();var source=d.Shapes.Single(s=>s.Kind==ShapeKind.Image);var original=d.Layers[source.Layer];var before=System.Text.Json.JsonSerializer.Serialize(d);
        var settings=new ImagePreparationSettings(new(mode,15,-20,1.3,true,140,false,1),.25);var next=settings.Apply(d,source.Id);
        Assert.Equal(original with {Raster=settings.Raster,Interval=settings.Interval},next.Layers[source.Layer]);Assert.Equal(d.Shapes,next.Shapes);
        var grid=RasterImages.EngravingGrid(source,next.Layers[source.Layer]);Assert.Equal((int)Math.Ceiling(source.Width/.25),grid.Width);Assert.Equal((int)Math.Ceiling(source.Height/.25),grid.Height);
        Assert.Equal(grid.Darkness,RasterImages.EngravingGrid(source,original with {Raster=settings.Raster,Interval=settings.Interval}).Darkness);
        var history=new EditorHistory();history.Reset(d);history.Apply(next);history.Undo();Assert.Equal(d.Layers,history.Document.Layers);history.Redo();Assert.Equal(next.Layers,history.Document.Layers);Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(d));
    }
    [Theory][InlineData(0)][InlineData(.001)][InlineData(101)][InlineData(double.NaN)]
    public void InvalidPreparationCannotModifyLayers(double interval)
    {
        var d=Sample();var source=d.Shapes.Single(s=>s.Kind==ShapeKind.Image);Assert.Throws<InvalidDataException>(()=>new ImagePreparationSettings(new(),interval).Apply(d,source.Id));Assert.Throws<InvalidDataException>(()=>new ImagePreparationSettings(new(),.1).Apply(d,Guid.NewGuid()));
    }
    [Fact]public void PreparationCancellationAndSampleLimitsAreEnforced()
    {
        var d=Sample();var source=d.Shapes.Single(s=>s.Kind==ShapeKind.Image);using var cancelled=new CancellationTokenSource();cancelled.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>RasterImages.EngravingGrid(source,d.Layers[source.Layer],cancelled.Token));Assert.Throws<InvalidDataException>(()=>RasterImages.EngravingGrid(source with {Width=10000,Height=10000},d.Layers[source.Layer] with {Interval=.01}));
    }
}
