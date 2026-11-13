using LaserCraft.Domain;
namespace LaserCraft.UnitTests;
public sealed class MaterialSearchTests
{
    private static MaterialPreset P(string name,string notes,LayerKind kind=LayerKind.Vector)=>new(Guid.NewGuid(),name,notes,3,LaserLayerSettings.Capture(new("Source","#000000",Mode:kind==LayerKind.Image?CutMode.Image:CutMode.Line,Kind:kind)));
    [Fact]public void SearchMatchesAllWordsAcrossNameAndNotesWithoutChangingLibrary()
    {
        var a=P("Birch plywood","Attzr scraps");var b=P("Plywood photo","Other laser",LayerKind.Image);var library=new MaterialLibrary(1,[b,a]);
        Assert.Equal(new[]{a},MaterialSearch.Find(library,"  PLYWOOD  ATTZR "));Assert.Equal(new[]{b},MaterialSearch.Find(library,"plywood",LayerKind.Image));Assert.Empty(MaterialSearch.Find(library,"attzr",LayerKind.Image));Assert.Equal(new[]{a,b},MaterialSearch.Find(library,null));Assert.Equal(new[]{b,a},library.Presets);
    }
    [Fact]public void SearchRetainsDuplicateNamesAndHandlesWhitespaceAndInvalidKinds()
    {
        var a=P("Recipe","notes");var b=P("Recipe","more");var library=new MaterialLibrary(1,[a,b]);Assert.Equal(2,MaterialSearch.Find(library,"\t \n").Count);Assert.Empty(MaterialSearch.Find(library,"absent"));Assert.Throws<InvalidDataException>(()=>MaterialSearch.Find(library,"",(LayerKind)999));
    }
}
