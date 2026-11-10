using System.Text.Json;
using LaserCraft.Domain;
namespace LaserCraft.UnitTests;
public sealed class MaterialPresetTests
{
    private static MaterialPreset Preset(CutLayer? layer = null) => new(Guid.NewGuid(), "Plywood test", "User recipe, qualify first", 3, LaserLayerSettings.Capture(layer ?? new("Source", "#FF0000", 800, 35, Mode:CutMode.Fill, Passes:3, AirAssist:true, Interval:.15)));
    [Fact] public void VectorRecipeChangesOnlyProcessSettingsAndUndoRestoresOutput()
    {
        var shape = new DesignShape(Guid.NewGuid(),ShapeKind.Rectangle,10,10,20,20,GroupId:Guid.NewGuid());
        var d=DesignDocument.Empty() with {Shapes=[shape],Layers=[new("Target","#123456",Output:false,Visible:false)]};var p=Preset();var next=p.Apply(d,0);
        Assert.Equal("Target",next.Layers[0].Name);Assert.Equal("#123456",next.Layers[0].Color);Assert.False(next.Layers[0].Output);Assert.False(next.Layers[0].Visible);Assert.Same(shape,next.Shapes[0]);
        Assert.Equal(p.Settings,LaserLayerSettings.Capture(next.Layers[0]));Assert.Equal(1200,d.Layers[0].Speed);
        var h=new EditorHistory();h.Reset(d);h.Apply(next);h.Undo();Assert.Equal(d.Layers,h.Document.Layers);h.Redo();Assert.Equal(next.Layers,h.Document.Layers);
    }
    [Fact] public void ImageRecipesRetainAllRasterSettingsAndRejectVectorTarget()
    {
        var raster=new RasterSettings(RasterMode.Stucki,10,-20,1.5,true,90,false,2);
        var p=Preset(new("Source image","#ABCDEF",Mode:CutMode.Image,Kind:LayerKind.Image,Raster:raster));
        var d=DesignDocument.Empty();d.Layers.Add(new("Target image","#123456",Mode:CutMode.Image,Kind:LayerKind.Image));var next=p.Apply(d,1);
        Assert.Equal(raster,next.Layers[1].Raster);Assert.Equal("Target image",next.Layers[1].Name);Assert.Throws<InvalidDataException>(()=>p.Apply(d,0));Assert.Throws<InvalidDataException>(()=>Preset().Apply(d,1));
    }
    [Fact] public void AppliedRecipeChangesPlannedPowerWithoutChangingGeometryOrOriginal()
    {
        var shape=new DesignShape(Guid.NewGuid(),ShapeKind.Rectangle,10,10,20,20);var d=DesignDocument.Empty() with {Shapes=[shape]};
        var p=Preset(new("Line","#000000",Speed:600,Power:10));var next=p.Apply(d,0);
        var before=ToolpathPlanner.Build(d);var after=ToolpathPlanner.Build(next);Assert.Equal(before.Segments.Count,after.Segments.Count);Assert.Equal(d.Shapes,next.Shapes);Assert.Equal(20,d.Layers[0].Power);Assert.Equal(10,next.Layers[0].Power);
    }
    [Theory] [InlineData(0)] [InlineData(-1)] [InlineData(1001)] [InlineData(double.NaN)]
    public void InvalidThicknessIsRejected(double t)=>Assert.Throws<InvalidDataException>(()=>(Preset() with {ThicknessMm=t}).Validate());
    [Fact] public void InvalidMetadataSettingsAndLibraryContentsAreRejected()
    {
        var p=Preset();foreach(var bad in new[]{p with {Id=Guid.Empty},p with {Name=""},p with {Name="bad\nname"},p with {Notes=new string('x',2001)},p with {Settings=p.Settings with {Speed=0}},p with {Settings=p.Settings with {Mode=CutMode.Image}}})Assert.Throws<InvalidDataException>(bad.Validate);
        Assert.Throws<InvalidDataException>(()=>new MaterialLibrary(2,[]).Validate());Assert.Throws<InvalidDataException>(()=>new MaterialLibrary(1,[p,p]).Validate());Assert.Throws<InvalidDataException>(()=>new MaterialLibrary(1,Enumerable.Range(0,501).Select(_=>p with {Id=Guid.NewGuid()}).ToList()).Validate());
        Assert.Throws<InvalidDataException>(()=>p.Apply(DesignDocument.Empty(),-1));(p with {ThicknessMm=null}).Validate();
    }
    [Fact] public void FileRoundTripBackupAndInvalidSavePreservePriorLibrary()
    {
        var folder=Path.Combine(Path.GetTempPath(),"LaserCraft-material-"+Guid.NewGuid().ToString("N"));var path=Path.Combine(folder,"materials.json");
        try
        {
            Assert.Empty(MaterialLibraryFile.Load(path).Presets);var p=Preset();MaterialLibraryFile.Save(path,new(1,[p]));Assert.Equal(p,MaterialLibraryFile.Load(path).Presets[0]);
            var updated=p with {Name="Updated",ThicknessMm=null};MaterialLibraryFile.Save(path,new(1,[updated]));Assert.Equal(p,MaterialLibraryFile.Load(path+".bak").Presets[0]);
            Assert.Throws<InvalidDataException>(()=>MaterialLibraryFile.Save(path,new(9,[])));Assert.Equal(updated,MaterialLibraryFile.Load(path).Presets[0]);Assert.Empty(Directory.GetFiles(folder,"*.tmp"));
            File.WriteAllText(path,"{broken");Assert.Throws<JsonException>(()=>MaterialLibraryFile.Load(path));Assert.Equal(p,MaterialLibraryFile.Load(path+".bak").Presets[0]);
        }
        finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [Fact] public void FileBoundsAndMissingSettingsFailExplicitly()
    {
        var path=Path.GetTempFileName();try
        {
            File.WriteAllText(path,new string(' ',2000001));Assert.Throws<InvalidDataException>(()=>MaterialLibraryFile.Load(path));
            File.WriteAllText(path,"{\"Version\":1,\"Presets\":[null]}");Assert.Throws<InvalidDataException>(()=>MaterialLibraryFile.Load(path));
            File.WriteAllText(path,"null");Assert.Throws<InvalidDataException>(()=>MaterialLibraryFile.Load(path));
        }
        finally {File.Delete(path);}
    }
}
