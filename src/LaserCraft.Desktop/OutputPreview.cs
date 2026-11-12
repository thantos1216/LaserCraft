using LaserCraft.Domain;
namespace LaserCraft.Desktop;

public sealed record PreparedOutput(DesignDocument Document,ToolpathPlan Plan,string Scope)
{
    public static HashSet<Guid>? ResolveSelection(bool selectedOnly,IEnumerable<Guid> selection)
    {
        if(!selectedOnly)return null;
        var ids=selection.ToHashSet();if(ids.Count==0)throw new InvalidDataException("Select artwork or choose All enabled artwork.");return ids;
    }
    public static PreparedOutput Create(DesignDocument document,IReadOnlySet<Guid>? selection,Func<DesignShape,CutLayer,CancellationToken,RasterGrid>? decoder=null,CancellationToken token=default)
    {
        var snapshot=document with {Shapes=document.Shapes.ToList(),Layers=document.Layers.ToList()};
        var plan=ToolpathPlanner.Build(snapshot,selection,decoder,token);
        if(plan.Segments.Count==0)throw new InvalidDataException("No enabled artwork in this output scope.");
        return new(snapshot,plan,selection is null?"All enabled artwork":"Selected objects only");
    }
}
