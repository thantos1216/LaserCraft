using LaserCraft.Domain;
namespace LaserCraft.UnitTests;

public sealed class LayerAndImageTests
{
    private static RasterAsset Asset() => new(Guid.NewGuid(), "image/png", "AAAA", 1, 1);
    private static DesignDocument ImageDocument()
    {
        var d = LayerEditing.Add(DesignDocument.Empty(), "Photo", LayerKind.Image);
        return d with { Shapes = [new(Guid.NewGuid(), ShapeKind.Image, 10, 20, 50, 40, 1, Asset())] };
    }
    [Fact]
    public void MovingLayersPreservesObjectAssociationAndSettings()
    {
        var d = ImageDocument(); d.Layers[1] = d.Layers[1] with { Speed = 500, Power = 44, AirAssist = true };
        var moved = LayerEditing.Move(d, 1, 0); Assert.Equal("Photo", moved.Layers[0].Name); Assert.Equal(500, moved.Layers[0].Speed); Assert.True(moved.Layers[0].AirAssist); Assert.Equal(0, moved.Shapes[0].Layer); Assert.Equal(d.Shapes[0].Id, moved.Shapes[0].Id);
    }
    [Fact]
    public void ReorderingChangesPreviewExecutionOrder()
    {
        var d = LayerEditing.Add(DesignDocument.Empty(), "Second", LayerKind.Vector);
        d.Shapes.Add(new(Guid.NewGuid(), ShapeKind.Rectangle, 10, 10, 10, 10, 0)); d.Shapes.Add(new(Guid.NewGuid(), ShapeKind.Rectangle, 100, 100, 10, 10, 1));
        Assert.Equal(10, ToolpathPlanner.Build(d).Segments.First(s => !s.Travel).From.X); var moved = LayerEditing.Move(d, 1, 0); Assert.Equal(100, ToolpathPlanner.Build(moved).Segments.First(s => !s.Travel).From.X);
    }
    [Fact]
    public void ImageCannotBeAssignedToVectorLayer()
    {
        var d = ImageDocument(); Assert.Throws<InvalidDataException>(() => LayerEditing.Assign(d, d.Shapes.Select(s => s.Id).ToHashSet(), 0));
    }
    [Fact]
    public void VectorCannotBeAssignedToImageLayer()
    {
        var d = ImageDocument(); var shape = new DesignShape(Guid.NewGuid(), ShapeKind.Rectangle, 0, 0, 10, 10); d.Shapes.Add(shape); Assert.Throws<InvalidDataException>(() => LayerEditing.Assign(d, new HashSet<Guid> { shape.Id }, 1));
    }
    [Fact]
    public void EachImageRequiresItsOwnLayer()
    {
        var d = ImageDocument(); d.Shapes.Add(d.Shapes[0] with { Id = Guid.NewGuid() }); Assert.Throws<InvalidDataException>(d.Validate);
    }
    [Fact]
    public void ImagesMoveBetweenDedicatedEmptyImageLayers()
    {
        var d = LayerEditing.Add(ImageDocument(), "Other photo", LayerKind.Image); var assigned = LayerEditing.Assign(d, d.Shapes.Select(s => s.Id).ToHashSet(), 2); Assert.Equal(2, assigned.Shapes[0].Layer);
    }
    [Fact]
    public void DeleteEmptyLayerRemapsRemainingReferences()
    {
        var d = ImageDocument(); var next = LayerEditing.RemoveEmpty(d, 0); Assert.Single(next.Layers); Assert.Equal(0, next.Shapes[0].Layer); Assert.Throws<InvalidDataException>(() => LayerEditing.RemoveEmpty(next, 0));
    }
    [Fact]
    public void ImageMetadataRoundTripsWithoutExternalFile()
    {
        var d = ImageDocument(); var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".lcraft");
        try { ProjectFile.Save(path, d); Assert.Equal(d.Shapes[0].Image, ProjectFile.Load(path).Shapes[0].Image); } finally { File.Delete(path); }
    }
    [Fact]
    public void ImageMirroringPreservesOriginalBytes()
    {
        var d = ImageDocument(); var mirrored = ShapeEditing.Mirror(d, d.Shapes.Select(s => s.Id).ToHashSet(), true); Assert.True(mirrored.Shapes[0].FlipX); Assert.Equal(d.Shapes[0].Image, mirrored.Shapes[0].Image);
    }
    [Fact] public void ImageOutputIsNotMisinterpretedAsAnEllipse() => Assert.Throws<InvalidDataException>(() => ToolpathPlanner.Build(ImageDocument()));
    [Fact]
    public void AirAssistIsCarriedOnlyByCutSegments()
    {
        var d = DesignDocument.Empty(); d.Layers[0] = d.Layers[0] with { AirAssist = true }; d.Shapes.Add(new(Guid.NewGuid(), ShapeKind.Rectangle, 10, 10, 10, 10)); var p = ToolpathPlanner.Build(d); Assert.All(p.Segments.Where(s => !s.Travel), s => Assert.True(s.AirAssist)); Assert.All(p.Segments.Where(s => s.Travel), s => Assert.False(s.AirAssist));
    }
    [Fact]
    public void LineBoundingBoxesDoNotInterceptAnImageInsideThem()
    {
        var d = ImageDocument(); var outline = new DesignShape(Guid.NewGuid(), ShapeKind.Rectangle, 0, 0, 100, 100); d.Shapes.Add(outline);
        Assert.False(ShapeEditing.HitTest(d, outline, 30, 30, 1)); Assert.True(ShapeEditing.HitTest(d, d.Shapes[0], 30, 30, 1)); Assert.True(ShapeEditing.HitTest(d, outline, 0, 30, 1));
    }
}
