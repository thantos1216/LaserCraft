namespace LaserCraft.Domain;
public static class GeometrySizing
{
    public static DesignDocument Resize(DesignDocument document,IReadOnlySet<Guid> ids,double x,double y,double width,double height)
    {
        document.Validate();var selected=document.Shapes.Where(s=>ids.Contains(s.Id)).ToArray();if(selected.Length==0||selected.Length!=ids.Count)throw new InvalidDataException("Select existing artwork to resize.");
        if(!double.IsFinite(width)||!double.IsFinite(height)||width<0||height<0)throw new InvalidDataException("Dimensions must be finite and nonnegative.");
        var b=ShapeEditing.Bounds(selected);if(b.Width==0&&width!=0||b.Height==0&&height!=0)throw new InvalidDataException("A zero-extent axis cannot be scaled to a nonzero size.");
        var sx=b.Width==0?1:width/b.Width;var sy=b.Height==0?1:height/b.Height;
        return ShapeEditing.Transform(document,ids,s=>s with {X=x+(s.X-b.X)*sx,Y=y+(s.Y-b.Y)*sy,Width=s.Width*sx,Height=s.Height*sy});
    }
    public static double MatchAspect(double changed,double originalChanged,double originalOther)
    {
        if(!double.IsFinite(originalChanged)||!double.IsFinite(originalOther)||originalChanged<=0||originalOther<=0||!double.IsFinite(changed)||changed<=0)throw new InvalidDataException("Aspect lock needs nonzero dimensions.");return changed*originalOther/originalChanged;
    }
}
