namespace LaserCraft.Domain;

public sealed record ImagePreparationSettings(RasterSettings Raster,double Interval)
{
    public void Validate()
    {
        if(Raster is null || !double.IsFinite(Interval)||Interval<.01||Interval>100)throw new InvalidDataException("Image interval must be between 0.01 and 100 mm.");
        Raster.Validate();
    }
    public DesignDocument Apply(DesignDocument document,Guid imageId)
    {
        Validate();document.Validate();var image=document.Shapes.SingleOrDefault(s=>s.Id==imageId&&s.Kind==ShapeKind.Image)??throw new InvalidDataException("The source image no longer exists.");
        var layers=document.Layers.ToList();layers[image.Layer]=layers[image.Layer] with {Raster=Raster,Interval=Interval};var next=document with {Layers=layers};next.Validate();return next;
    }
}
