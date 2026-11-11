using System.Text.Json;
using LaserCraft.Domain;
namespace LaserCraft.UnitTests;
public sealed class PassOrderTests
{
    private static DesignDocument Document(PassOrderMode order,CutMode mode=CutMode.Line)=>DesignDocument.Empty() with {Shapes=[new(Guid.NewGuid(),ShapeKind.Rectangle,10,10,4,4),new(Guid.NewGuid(),ShapeKind.Rectangle,30,10,4,4)],Layers=[new("Cuts","#000000",Passes:5,Mode:mode,Interval:1,PassOrder:order)]};
    [Theory][InlineData(PassOrderMode.WholeLayer)][InlineData(PassOrderMode.PerObject)]
    public void FivePassesProduceFiveVisitsToEachObjectInChosenOrder(PassOrderMode order)
    {
        var plan=ToolpathPlanner.Build(Document(order));var cuts=plan.Segments.Where(s=>!s.Travel).ToArray();Assert.Equal(40,cuts.Length);
        var visits=cuts.Where((_,i)=>i%4==0).Select(s=>s.From.X<20?"A":"B").ToArray();
        Assert.Equal(order==PassOrderMode.WholeLayer?new[]{"A","B","A","B","A","B","A","B","A","B"}:new[]{"A","A","A","A","A","B","B","B","B","B"},visits);
        Assert.Equal(160,plan.CutLength);
    }
    [Theory][InlineData(PassOrderMode.WholeLayer)][InlineData(PassOrderMode.PerObject)]
    public void FillAndSelectedOnlyRetainEveryPass(PassOrderMode order)
    {
        var d=Document(order,CutMode.Fill);var plan=ToolpathPlanner.Build(d);Assert.Equal(40,plan.Segments.Count(s=>!s.Travel));
        var selected=ToolpathPlanner.Build(d,new HashSet<Guid>{d.Shapes[1].Id});Assert.Equal(20,selected.Segments.Count(s=>!s.Travel));Assert.All(selected.Segments.Where(s=>!s.Travel),s=>Assert.True(s.From.X>=30));
        if(order==PassOrderMode.PerObject)Assert.All(plan.Segments.Where(s=>!s.Travel).Take(20),s=>Assert.True(s.From.X<20));
    }
    [Fact]public void RasterPassesDecodeOnceAndPersistOrderThroughProjectAndPreset()
    {
        var image=new RasterAsset(Guid.NewGuid(),"image/png","AAAA",1,1);var d=DesignDocument.Empty() with {Shapes=[new(Guid.NewGuid(),ShapeKind.Image,10,10,1,1,Image:image)],Layers=[new("Image","#000000",Mode:CutMode.Image,Kind:LayerKind.Image,Passes:5,Interval:1,PassOrder:PassOrderMode.PerObject)]};int calls=0;
        var plan=ToolpathPlanner.Build(d,rasterDecoder:(_,_,_)=>{calls++;return new RasterGrid(1,1,[255]);});Assert.Equal(1,calls);Assert.Equal(5,plan.Segments.Count(s=>!s.Travel));
        var copy=JsonSerializer.Deserialize<DesignDocument>(JsonSerializer.Serialize(d))!;copy.Validate();Assert.Equal(PassOrderMode.PerObject,copy.Layers[0].PassOrder);
        Assert.Equal(PassOrderMode.PerObject,LaserLayerSettings.Capture(d.Layers[0]).Apply(d.Layers[0] with {PassOrder=PassOrderMode.WholeLayer}).PassOrder);
        var legacy=JsonSerializer.Deserialize<CutLayer>("{\"Name\":\"Old\",\"Color\":\"#000000\"}")!;Assert.Equal(PassOrderMode.WholeLayer,legacy.PassOrder);
        Assert.Throws<InvalidDataException>(()=>(d with {Layers=[d.Layers[0] with {PassOrder=(PassOrderMode)99}]}).Validate());
    }
}
