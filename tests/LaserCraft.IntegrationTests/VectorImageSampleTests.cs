using LaserCraft.Desktop;
using LaserCraft.Domain;
namespace LaserCraft.IntegrationTests;
public sealed class VectorImageSampleTests
{
    [Fact]public void PortableSampleDecodesAndPlansVectorAndRasterLayers()
    {
        var d=ProjectFile.Load(Path.Combine(AppContext.BaseDirectory,"samples","vector-and-image-demo.lcraft"));RasterImages.Verify(d);
        var plan=JobFootprint.TrimInitialTravel(ToolpathPlanner.Build(d,null,RasterImages.EngravingGrid));var b=JobFootprint.Measure(plan);Assert.Equal(25,b.Width);Assert.Equal(10,b.Height);
        Assert.Contains(plan.Segments,s=>s.Layer==0&&!s.Travel&&s.Power>0);Assert.Contains(plan.Segments,s=>s.Layer==1&&!s.Travel&&s.Power>0);
        var profile=DeviceProfile.Simulator() with {BedWidth=430,BedHeight=430};var program=GrblExport.Write(plan,profile,new(-100,-100));Assert.Contains("S100",program);Assert.Contains("G53 G1",program);
        var frame=GrblExport.Write(JobFootprint.Frame(plan),profile,new(-100,-100));Assert.DoesNotContain("S100",frame);Assert.DoesNotContain("M8",frame);
    }
}
