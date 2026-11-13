using LaserCraft.Desktop;
using LaserCraft.Domain;
namespace LaserCraft.IntegrationTests;
public sealed class TraceFillRegressionTests
{
    [Fact]public void PhotoContoursCanSwitchBetweenLineAndFillAndTextResizeRemainsEditable()
    {
        var asset=RasterImages.Import(File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"samples","nasa-blue-marble.jpg")));var image=new DesignShape(Guid.NewGuid(),ShapeKind.Image,10,10,200,200,1,asset);
        var gray=RasterImages.EngravingGrid(image,new("Gray","#000000",Mode:CutMode.Image,Kind:LayerKind.Image,Interval:200d/512,Raster:new(RasterMode.Grayscale)));
        var trace=PhotoTracing.Build(gray,image,new(Cutoff:20,Threshold:160,NoiseSmoothing:.8,Smoothness:.5,Optimize:.7),8).Trace.Path;Assert.NotNull(trace);
        var d=DesignDocument.Empty() with {Shapes=[trace],Layers=[new("Trace","#000000",Mode:CutMode.Fill,Interval:.1)]};Assert.NotEmpty(PreparedOutput.Create(d,null).Plan.Segments);Assert.NotEmpty(PreparedOutput.Create(d with {Layers=[d.Layers[0] with {Mode=CutMode.Line}]},null).Plan.Segments);
        var font=FontOutlines.Create(new("LaserCraft","Arial"));var t=DesignDocument.Empty() with {Shapes=[font with {X=10,Y=10}]};var size=GeometrySizing.Resize(t,new HashSet<Guid>{font.Id},10,10,font.Width*2,font.Height*2);Assert.Equal(font.Width*2,size.Shapes[0].Width,8);Assert.Equal(ToolpathPlanner.Build(t).CutLength*2,ToolpathPlanner.Build(size).CutLength,6);Assert.NotNull(size.Shapes[0].Text);
    }
}
