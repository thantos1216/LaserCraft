using LaserCraft.Domain;
using System.Collections.Immutable;
namespace LaserCraft.UnitTests;
public sealed class WorkspaceFixTests
{
    [Fact]public void DeletedImageLayersPruneAndRemapWithoutChangingOtherSettings()
    {
        var image=new DesignShape(Guid.NewGuid(),ShapeKind.Image,0,0,10,10,1,new(Guid.NewGuid(),"image/png","AAAA",1,1));var rect=new DesignShape(Guid.NewGuid(),ShapeKind.Rectangle,20,20,10,10,2);
        var d=DesignDocument.Empty() with {Shapes=[image,rect],Layers=[new("Unused","#000000"),new("Image","#111111",Mode:CutMode.Image,Kind:LayerKind.Image),new("Cut","#222222",Speed:500,Passes:3)]};
        var next=LayerEditing.PruneUnused(d with {Shapes=[rect]}).Document;Assert.Single(next.Layers);Assert.Equal(d.Layers[2],next.Layers[0]);Assert.Equal(0,next.Shapes[0].Layer);Assert.Equal(rect.Id,next.Shapes[0].Id);Assert.Single(LayerEditing.VisibleIndices(next));
        Assert.Equal(0,LayerEditing.PruneUnused(d with {Shapes=[rect]},2).ActiveLayer);
        var empty=LayerEditing.PruneUnused(next with {Shapes=[]}).Document;Assert.Empty(LayerEditing.VisibleIndices(empty));empty.Validate();Assert.Empty(LayerEditing.VisibleIndices(DesignDocument.Empty()));
        var draft=LayerEditing.Add(empty,"User layer",LayerKind.Vector);Assert.Single(LayerEditing.VisibleIndices(draft));Assert.Equal("User layer",LayerEditing.PruneUnused(draft).Document.Layers[0].Name);
        var usedDraft=LayerEditing.PruneUnused(draft with {Shapes=[rect with {Layer=1}]},1).Document;Assert.False(usedDraft.Layers[0].KeepEmpty);Assert.Empty(LayerEditing.VisibleIndices(LayerEditing.PruneUnused(usedDraft with {Shapes=[]}).Document));
    }
    [Fact]public void TextResizePreservesSourceAndGroupAndScalesActualPaths()
    {
        var s=VectorPaths.Create([new([new(10,20),new(30,20),new(30,30),new(10,30)],true)]) with {Text=new("Test","Arial",NaturalWidth:20,NaturalHeight:10),GroupId=Guid.NewGuid()};var d=DesignDocument.Empty() with {Shapes=[s]};
        var resized=GeometrySizing.Resize(d,new HashSet<Guid>{s.Id},15,25,40,20);var shape=resized.Shapes[0];Assert.Equal((40d,20d),(shape.Width,shape.Height));Assert.Same(s.Text,shape.Text);Assert.Equal(s.GroupId,shape.GroupId);Assert.Equal(new PointMm(55,45),VectorPaths.World(shape)[0].Points[2]);Assert.Equal(20,GeometrySizing.MatchAspect(40,20,10));
        var edited=TextEditing.Install(resized,s with {Width=30,Height=10,Text=s.Text with {NaturalWidth=30}},s.Id);Assert.Equal(60,edited.Shapes[0].Width);Assert.Equal(20,edited.Shapes[0].Height);Assert.True(ShapeEditing.HitTest(d,s,20,25,0));
    }
    [Fact]public void SparseComplexFillUsesOnlyActiveEdgesAndPreservesHoleIntervals()
    {
        PathContour Rect(double x,double y,double w,double h)=>new([new(x,y),new(x+w,y),new(x+w,y+h),new(x,y+h)],true);
        // 1000 disjoint vertical regions would require > 8 million old row/edge probes.
        var shape=VectorPaths.Create(Enumerable.Range(0,1000).Select(i=>Rect(10,10+i*.2,10,.1)));var d=DesignDocument.Empty() with {Shapes=[shape],Layers=[new("Fill","#000000",Mode:CutMode.Fill,Interval:.1)]};var p=ToolpathPlanner.Build(d);Assert.Equal(10000,p.CutLength,6);
        var holes=VectorPaths.Create([Rect(10,10,20,20),Rect(15,15,10,10)]);var filled=ToolpathPlanner.Build(d with {Shapes=[holes],Layers=[d.Layers[0] with {Interval=1}]});Assert.Equal(300,filled.CutLength,8);
    }
    [Fact]public void MaterialGridInterpolatesEndpointsPassesAndKeepsCellsEditable()
    {
        var result=MaterialTests.Create(430,430,new(Rows:3,Columns:3,RowAxis:MaterialTestAxis.Passes,ColumnAxis:MaterialTestAxis.Power,RowMinimum:1,RowMaximum:5,ColumnMinimum:10,ColumnMaximum:90,Mode:CutMode.Fill,AirAssist:true));
        Assert.Equal(9,result.Document.Shapes.Count);Assert.Single(result.Document.Shapes.Select(s=>s.GroupId).Distinct());Assert.NotNull(result.Document.Shapes[0].GroupId);Assert.Equal(new[]{1,3,5},result.Cells.Select(c=>c.Passes).Distinct());Assert.Equal(new[]{10d,50d,90d},result.Cells.Take(3).Select(c=>c.Power));Assert.All(result.Document.Layers,l=>{Assert.True(l.AirAssist);Assert.Equal(CutMode.Fill,l.Mode);});Assert.Equal(9,result.Document.Shapes.Select(s=>s.Layer).Distinct().Count());Assert.NotEmpty(ToolpathPlanner.Build(result.Document).Segments);
        var h=new EditorHistory();h.Reset(DesignDocument.Empty());h.Apply(result.Document);h.Undo();Assert.Empty(h.Document.Shapes);h.Redo();Assert.Equal(9,h.Document.Shapes.Count);
    }
    [Fact]public void MaterialGridRejectsDuplicateAxesBoundsAndInvalidProcessRanges()
    {
        Assert.Throws<InvalidDataException>(()=>MaterialTests.Create(430,430,new(Rows:17,Columns:17)));Assert.Throws<InvalidDataException>(()=>MaterialTests.Create(430,430,new(RowAxis:MaterialTestAxis.Speed)));Assert.Throws<InvalidDataException>(()=>MaterialTests.Create(20,20,new()));Assert.Throws<InvalidDataException>(()=>MaterialTests.Create(430,430,new(RowMinimum:-1)));Assert.Throws<InvalidDataException>(()=>MaterialTests.Create(430,430,new(RowMaximum:101)));
        var one=MaterialTests.Create(430,430,new(Rows:1,Columns:1));Assert.Single(one.Cells);Assert.Equal(10,one.Cells[0].Power);Assert.Equal(300,one.Cells[0].Speed);
    }
}
