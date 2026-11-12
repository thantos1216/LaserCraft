using LaserCraft.Domain;
using System.Collections.Immutable;
namespace LaserCraft.UnitTests;
public sealed class CircularArrayTests
{
    private static HashSet<Guid> Ids(DesignDocument d)=>d.Shapes.Select(s=>s.Id).ToHashSet();
    private static DesignDocument Example()=>DesignDocument.Empty() with {Shapes=[new(Guid.NewGuid(),ShapeKind.Rectangle,95,45,10,10)]};
    [Fact]public void FullCircleHasNoDuplicateEndpointAndPartialArcIncludesItsEnd()
    {
        var d=Example();var before=System.Text.Json.JsonSerializer.Serialize(d);var ids=Ids(d);
        var full=CircularArrays.Create(d,ids,4,new(50,50),360,false);Assert.Equal(4,full.Document.Shapes.Count);Assert.Same(d.Shapes[0],full.Document.Shapes[0]);
        Assert.Equal(new[]{(95d,45d),(45d,95d),(-5d,45d),(45d,-5d)},full.Document.Shapes.Select(s=>(s.X,s.Y)));
        Assert.Equal(90,CircularArrays.Measure(d,ids,4,new(50,50),360,false).StepDegrees);
        var arc=CircularArrays.Create(d,ids,3,new(50,50),-180,false);Assert.Equal((45d,-5d),(arc.Document.Shapes[1].X,arc.Document.Shapes[1].Y));Assert.Equal((-5d,45d),(arc.Document.Shapes[2].X,arc.Document.Shapes[2].Y));
        Assert.Equal(before,System.Text.Json.JsonSerializer.Serialize(d));Assert.Equal(4,full.Selection.Distinct().Count());
    }
    [Fact]public void RotatedCellsPreserveContoursLayerAndPerimeterAndRemapGroups()
    {
        var group=Guid.NewGuid();var a=VectorPaths.Create([new([new(95,45),new(105,45),new(105,55),new(95,55)],true),new([new(98,48),new(102,48),new(102,52),new(98,52)],true)],groupId:group) with {FlipX=true};
        var b=new DesignShape(Guid.NewGuid(),ShapeKind.Rectangle,110,45,5,10,GroupId:group);var d=DesignDocument.Empty() with {Shapes=[a,b]};
        var result=CircularArrays.Create(d,Ids(d),4,new(50,50),360,true).Document;
        Assert.Equal(8,result.Shapes.Count);Assert.All(result.Shapes.Skip(2),s=>Assert.Equal(ShapeKind.Path,s.Kind));Assert.Equal(2,result.Shapes[2].Path!.Contours.Length);Assert.False(result.Shapes[2].FlipX);
        var groups=result.Shapes.Chunk(2).Select(c=>{Assert.Equal(c[0].GroupId,c[1].GroupId);return c[0].GroupId;}).ToArray();Assert.Equal(4,groups.Distinct().Count());Assert.Equal(group,groups[0]);
        var offBed=result with {Shapes=result.Shapes.Select(s=>s with {X=s.X+100,Y=s.Y+100}).ToList()};var source=d with {Shapes=d.Shapes.Select(s=>s with {X=s.X+100,Y=s.Y+100}).ToList()};
        Assert.Equal(4*ToolpathPlanner.Build(source).CutLength,ToolpathPlanner.Build(offBed).CutLength,6);
    }
    [Fact]public void ImagesKeepPixelsMirrorsAndSettingsOnDedicatedLayers()
    {
        var asset=new RasterAsset(Guid.NewGuid(),"image/png","AAAA",1,1);var d=DesignDocument.Empty() with {Shapes=[new(Guid.NewGuid(),ShapeKind.Image,95,45,10,10,1,asset,true)]};d.Layers.Add(new("Photo","#123456",Speed:750,Power:35,Mode:CutMode.Image,Passes:3,AirAssist:true,Kind:LayerKind.Image,Raster:new(){Brightness=10}));
        var result=CircularArrays.Create(d,Ids(d),4,new(50,50),360,false).Document;
        Assert.Equal(5,result.Layers.Count);Assert.Equal(4,result.Shapes.Select(s=>s.Layer).Distinct().Count());foreach(var s in result.Shapes){Assert.Same(asset,s.Image);Assert.True(s.FlipX);Assert.Equal(d.Layers[1],result.Layers[s.Layer] with {Name="Photo"});}
        Assert.Throws<InvalidDataException>(()=>CircularArrays.Create(d,Ids(d),4,new(50,50),360,true));
    }
    [Theory][InlineData(1,360)][InlineData(100001,360)][InlineData(2,0)][InlineData(2,361)][InlineData(2,double.NaN)]
    public void InvalidParametersAreRejected(int count,double sweep){var d=Example();Assert.Throws<InvalidDataException>(()=>CircularArrays.Create(d,Ids(d),count,new(50,50),sweep,false));Assert.Single(d.Shapes);}
    [Fact]public void ExpandedGeometryAndCoordinatesAreCheckedBeforeCopies()
    {
        var d=Example();Assert.Throws<InvalidDataException>(()=>CircularArrays.Measure(d,Ids(d),100000,new(50,50),360,true));Assert.Throws<InvalidDataException>(()=>CircularArrays.Measure(d,Ids(d),4,new(10000,0),360,false));Assert.Throws<InvalidDataException>(()=>CircularArrays.Measure(d,Ids(d),4,new(double.NaN,0),360,false));Assert.Throws<InvalidDataException>(()=>CircularArrays.Measure(d,new HashSet<Guid>{Guid.NewGuid()},4,new(50,50),360,false));
        var line=VectorPaths.Create([new(Enumerable.Range(0,100000).Select(i=>new PointMm(i%2,i/100000d)).ToImmutableArray())]);d=d with {Shapes=[line]};Assert.Throws<InvalidDataException>(()=>CircularArrays.Measure(d,Ids(d),3,new(50,50),360,true));
    }
    [Fact]public void HistoryAndNativePersistenceRetainTheCreatedArray()
    {
        var d=Example();var next=CircularArrays.Create(d,Ids(d),4,new(150,150),180,true).Document;var h=new EditorHistory();h.Reset(d);h.Apply(next);h.Undo();Assert.Single(h.Document.Shapes);h.Redo();Assert.Equal(4,h.Document.Shapes.Count);
        var path=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".lcraft");try{ProjectFile.Save(path,next);var loaded=ProjectFile.Load(path);Assert.Equal(next.Shapes.Select(s=>s.Id),loaded.Shapes.Select(s=>s.Id));Assert.Equal(ToolpathPlanner.Build(next).Segments,ToolpathPlanner.Build(loaded).Segments);}finally{File.Delete(path);}
    }
}
