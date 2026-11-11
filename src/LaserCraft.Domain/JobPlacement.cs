namespace LaserCraft.Domain;
public enum StartFromMode { AbsoluteCoordinates, CurrentPosition, UserOrigin }
public enum JobOriginAnchor { TopLeft, TopCenter, TopRight, MiddleLeft, Center, MiddleRight, BottomLeft, BottomCenter, BottomRight }
public sealed record JobPlacementRequest(StartFromMode Mode=StartFromMode.CurrentPosition, JobOriginAnchor Anchor=JobOriginAnchor.BottomLeft);
public sealed record ResolvedJobPlacement(PointMm Offset, BoundsMm MachineBounds);
public static class JobPlacement
{
    public static PointMm MapToBed(DeviceProfile p,PointMm point)=>new(p.Origin is MachineOrigin.TopRight or MachineOrigin.BottomRight?p.BedWidth-point.X:point.X,p.Origin is MachineOrigin.BottomLeft or MachineOrigin.BottomRight?p.BedHeight-point.Y:point.Y);
    public static PointMm BedTarget(DeviceProfile p,MachineTravel travel,PointMm target)
    {
        if(!double.IsFinite(target.X)||!double.IsFinite(target.Y)||target.X<0||target.Y<0||target.X>p.BedWidth||target.Y>p.BedHeight)throw new InvalidDataException("Bed target is outside the profile bed.");
        return new(travel.MinX+target.X,travel.MinY+target.Y);
    }
    public static ResolvedJobPlacement Resolve(ToolpathPlan plan,DeviceProfile p,MachineTravel travel,PointMm current,PointMm? userOrigin,JobPlacementRequest request)
    {
        p.Validate();if(!Enum.IsDefined(request.Mode)||!Enum.IsDefined(request.Anchor))throw new InvalidDataException("Unknown job placement mode/anchor.");
        var bounds=JobFootprint.Measure(plan);var mapped=plan.Segments.SelectMany(s=>new[]{MapToBed(p,s.From),MapToBed(p,s.To)}).ToArray();var x=mapped.Min(v=>v.X);var y=mapped.Min(v=>v.Y);var width=mapped.Max(v=>v.X)-x;var height=mapped.Max(v=>v.Y)-y;
        PointMm offset;
        if(request.Mode==StartFromMode.AbsoluteCoordinates)offset=new(travel.MinX,travel.MinY);
        else
        {
            var target=request.Mode==StartFromMode.CurrentPosition?current:userOrigin??throw new InvalidDataException("Set User Origin in this connection first.");
            if(!double.IsFinite(target.X)||!double.IsFinite(target.Y))throw new InvalidDataException("Invalid placement reference.");
            var index=(int)request.Anchor;var anchor=MapToBed(p,new(bounds.X+(index%3)*bounds.Width/2,bounds.Y+(index/3)*bounds.Height/2));offset=new(target.X-anchor.X,target.Y-anchor.Y);
        }
        var output=new BoundsMm(x+offset.X,y+offset.Y,width,height);
        if(!travel.Contains(new(output.X,output.Y),output))throw new InvalidDataException(FormattableString.Invariant($"Placed footprint X[{output.X:0.###},{output.X+width:0.###}], Y[{output.Y:0.###},{output.Y+height:0.###}] exceeds machine travel. Check Start From, origin and placement."));
        return new(offset,output);
    }
}
