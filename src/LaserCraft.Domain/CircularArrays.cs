using System.Collections.Immutable;
namespace LaserCraft.Domain;

public sealed record CircularArrayLayout(int Count,double StepDegrees,int AddedObjects,int AddedLayers,BoundsMm Bounds);
public static class CircularArrays
{
    private static DesignShape[] Select(DesignDocument d,IReadOnlySet<Guid> ids)
    {
        d.Validate();var selected=d.Shapes.Where(s=>ids.Contains(s.Id)).ToArray();
        if(selected.Length==0||selected.Length!=ids.Count)throw new InvalidDataException("Select existing artwork to repeat.");return selected;
    }
    private static double Step(int count,double sweep)
    {
        if(count<2||count>100000||!double.IsFinite(sweep)||sweep==0||Math.Abs(sweep)>360)throw new InvalidDataException("Use 2–100000 total cells and a nonzero sweep between -360 and 360 degrees.");
        return sweep/(Math.Abs(sweep)==360?count:count-1);
    }
    private static Func<PointMm,PointMm> Rotation(PointMm center,double degrees)
    {
        var angle=degrees*Math.PI/180;var c=Math.Cos(angle);var s=Math.Sin(angle);
        if(Math.Abs(degrees/90-Math.Round(degrees/90))<1e-12){c=Math.Round(c);s=Math.Round(s);}
        return p=>new(center.X+(p.X-center.X)*c-(p.Y-center.Y)*s,center.Y+(p.X-center.X)*s+(p.Y-center.Y)*c);
    }
    private static DesignShape Transform(DesignShape source,PointMm center,PointMm cellCenter,double angle,bool rotate)
    {
        var turn=Rotation(center,angle);
        if(rotate)return VectorPaths.Create(VectorPaths.World(VectorPaths.Convert(source)).Select(c=>c with {Points=c.Points.Select(turn).ToImmutableArray()}),source.Layer,source.Id,source.GroupId);
        var next=turn(cellCenter);return source with {X=source.X+next.X-cellCenter.X,Y=source.Y+next.Y-cellCenter.Y};
    }
    public static CircularArrayLayout Measure(DesignDocument d,IReadOnlySet<Guid> ids,int count,PointMm center,double sweep,bool rotate)
    {
        var selected=Select(d,ids);var step=Step(count,sweep);
        if(!double.IsFinite(center.X)||!double.IsFinite(center.Y)||Math.Abs(center.X)>10000||Math.Abs(center.Y)>10000)throw new InvalidDataException("Center must be within the finite project coordinate range.");
        if(rotate&&selected.Any(s=>s.Kind==ShapeKind.Image))throw new InvalidDataException("Rotating cells supports vectors only; disable rotation to repeat images.");
        var budget=Arrangement.CheckCopyBudget(d,selected,count-1,rotate);var bounds=ShapeEditing.Bounds(selected);var cell=new PointMm(bounds.X+bounds.Width/2,bounds.Y+bounds.Height/2);
        var minX=bounds.X;var minY=bounds.Y;var maxX=bounds.X+bounds.Width;var maxY=bounds.Y+bounds.Height;
        for(var i=1;i<count;i++)foreach(var source in selected)
        {
            var shape=Transform(source,center,cell,i*step,rotate);
            if(!double.IsFinite(shape.X)||!double.IsFinite(shape.Y)||Math.Abs(shape.X)>10000||Math.Abs(shape.Y)>10000||shape.Width>10000||shape.Height>10000)throw new InvalidDataException("Array positions exceed the project coordinate range.");
            minX=Math.Min(minX,shape.X);minY=Math.Min(minY,shape.Y);maxX=Math.Max(maxX,shape.X+shape.Width);maxY=Math.Max(maxY,shape.Y+shape.Height);
        }
        return new(count,step,budget.Objects,budget.Layers,new(minX,minY,maxX-minX,maxY-minY));
    }
    public static ArrangementResult Create(DesignDocument d,IReadOnlySet<Guid> ids,int count,PointMm center,double sweep,bool rotate)
    {
        var layout=Measure(d,ids,count,center,sweep,rotate);var selected=d.Shapes.Where(s=>ids.Contains(s.Id)).ToArray();var b=ShapeEditing.Bounds(selected);var cellCenter=new PointMm(b.X+b.Width/2,b.Y+b.Height/2);
        var shapes=d.Shapes.ToList();var layers=d.Layers.ToList();var selection=selected.Select(s=>s.Id).ToList();
        for(var i=1;i<count;i++)
        {
            var cell=new List<DesignShape>(selected.Length);
            foreach(var source in selected)
            {
                var shape=Transform(source,center,cellCenter,i*layout.StepDegrees,rotate) with {Id=Guid.NewGuid()};
                if(source.Kind==ShapeKind.Image){shape=shape with {Layer=layers.Count};layers.Add(d.Layers[source.Layer] with {Name=d.Layers[source.Layer].Name+$" [arc {i+1}]"});}
                cell.Add(shape);selection.Add(shape.Id);
            }
            shapes.AddRange(ShapeGroups.RemapCopies(cell));
        }
        var next=d with {Shapes=shapes,Layers=layers};next.Validate();return new(next,selection.AsReadOnly());
    }
}
