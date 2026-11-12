using LaserCraft.Desktop;
using LaserCraft.Domain;
namespace LaserCraft.IntegrationTests;
public sealed class OutputPreviewTests
{
    [Fact]public void AllIsDefaultScopeAndSelectedOnlyRequiresASelection()
    {
        var a=new DesignShape(Guid.NewGuid(),ShapeKind.Rectangle,10,10,5,5);var b=a with {Id=Guid.NewGuid(),X=30};var d=DesignDocument.Empty() with {Shapes=[a,b]};
        var all=PreparedOutput.Create(d,PreparedOutput.ResolveSelection(false,[a.Id]));var selected=PreparedOutput.Create(d,PreparedOutput.ResolveSelection(true,[a.Id]));
        Assert.Equal(40,all.Plan.CutLength);Assert.Equal(20,selected.Plan.CutLength);Assert.Equal("All enabled artwork",all.Scope);Assert.Equal("Selected objects only",selected.Scope);
        Assert.Throws<InvalidDataException>(()=>PreparedOutput.ResolveSelection(true,[]));Assert.Throws<InvalidDataException>(()=>PreparedOutput.Create(d with {Layers=[d.Layers[0] with {Output=false}]},null));
    }
    [Fact]public void PreparingRasterPreviewUsesTheSamePlanAsOutputWithoutMutatingTheSource()
    {
        var path=Path.Combine(AppContext.BaseDirectory,"samples","vector-and-image-demo.lcraft");var d=ProjectFile.Load(path);var before=System.Text.Json.JsonSerializer.Serialize(d);
        var prepared=PreparedOutput.Create(d,null,RasterImages.EngravingGrid);var actual=ToolpathPlanner.Build(d,rasterDecoder:RasterImages.EngravingGrid);
        Assert.Equal(actual.Segments,prepared.Plan.Segments);Assert.Contains(prepared.Plan.Segments,s=>s.Layer==1&&!s.Travel&&s.Power>0);Assert.NotSame(d.Shapes,prepared.Document.Shapes);Assert.NotSame(d.Layers,prepared.Document.Layers);
        Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(d));
    }
    [Fact]public void PreviewPlaybackSeeksPausesAndRestartsWithoutChangingPlan()
    {
        var plan=new ToolpathPlan([new(new(0,0),new(100,0),0,false,600)]);var p=new JobSimulation(plan);p.Seek(4);Assert.Equal(SimulationState.Paused,p.State);p.Start();p.Advance(1);Assert.Equal(5,p.Elapsed);p.Seek(20);Assert.Equal(SimulationState.Completed,p.State);Assert.Equal(10,p.Elapsed);p.Start();Assert.Equal(0,p.Elapsed);p.Seek(-1);Assert.Equal(0,p.Elapsed);Assert.Throws<ArgumentOutOfRangeException>(()=>p.Seek(double.NaN));Assert.Same(plan,p.Plan);
    }
}
