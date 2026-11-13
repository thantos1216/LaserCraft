using LaserCraft.Desktop;
using LaserCraft.Domain;
namespace LaserCraft.IntegrationTests;
public sealed class LargeImagePreviewTests
{
    private static DesignShape Photo(double width=400,double height=400)
    {
        var asset=RasterImages.Import(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"samples","nasa-blue-marble.jpg")));
        return new(Guid.NewGuid(),ShapeKind.Image,0,0,width,height,1,asset);
    }
    [Fact]public void OversizedPhotoStillPreviewsToneChangesWithoutChangingOutputResolution()
    {
        var photo=Photo();var settings=new ImagePreparationSettings(new(RasterMode.Stucki),.1);var before=RasterImages.PrepareEditPreview(photo,settings);
        var edited=RasterImages.PrepareEditPreview(photo,settings with {Raster=settings.Raster with {Brightness=55,Contrast=16,Gamma=1.9}});
        Assert.Equal((800,800),(before.Original.Width,before.Original.Height));Assert.Equal((4000,4000),(edited.OutputWidth,edited.OutputHeight));Assert.True(edited.Reduced);Assert.False(edited.OutputSupported);
        Assert.Equal(before.Original.Darkness,edited.Original.Darkness);Assert.False(before.Adjusted.Darkness.SequenceEqual(edited.Adjusted.Darkness));Assert.True(edited.Adjusted.Darkness.Average(v=>(double)v)<before.Adjusted.Darkness.Average(v=>(double)v));
        Assert.All(edited.Laser.Darkness,v=>Assert.True(v is 0 or 255));Assert.Contains(edited.Adjusted.Darkness,v=>v>0&&v<255);
        Assert.Throws<InvalidDataException>(()=>RasterImages.EngravingGrid(photo,new("Image","#000000",Mode:CutMode.Image,Kind:LayerKind.Image,Interval:.1,Raster:settings.Raster)));
    }
    [Fact]public void SmallLaserPreviewMatchesExactOutputSamples()
    {
        var photo=Photo(30,20);var settings=new ImagePreparationSettings(new(RasterMode.Atkinson,10,-5,1.2,true),.1);var preview=RasterImages.PrepareEditPreview(photo,settings);
        Assert.False(preview.Reduced);Assert.True(preview.OutputSupported);Assert.Equal((300,200),(preview.Laser.Width,preview.Laser.Height));
        Assert.Equal(RasterImages.EngravingGrid(photo,new("Image","#000000",Mode:CutMode.Image,Kind:LayerKind.Image,Interval:settings.Interval,Raster:settings.Raster)).Darkness,preview.Laser.Darkness);
    }
    [Fact]public void PreviewPreservesAspectAndGrayscaleModeAndHonorsCancellation()
    {
        var photo=Photo(1000,250);var settings=new ImagePreparationSettings(new(RasterMode.Grayscale),.01);var p=RasterImages.PrepareEditPreview(photo,settings);Assert.Equal((800,200),(p.Laser.Width,p.Laser.Height));Assert.Same(p.Adjusted,p.Laser);
        using var stop=new CancellationTokenSource();stop.Cancel();Assert.ThrowsAny<OperationCanceledException>(()=>RasterImages.PrepareEditPreview(photo,settings,stop.Token));
    }
}
