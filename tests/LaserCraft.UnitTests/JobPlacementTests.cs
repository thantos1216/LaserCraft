using LaserCraft.Domain;
namespace LaserCraft.UnitTests;
public sealed class JobPlacementTests
{
    private static DeviceProfile Profile=>DeviceProfile.Simulator() with {BedWidth=430,BedHeight=430};
    private static ToolpathPlan Plan=>JobFootprint.TrimInitialTravel(ToolpathPlanner.Build(DesignDocument.Empty() with {Width=430,Height=430,Shapes=[new(Guid.NewGuid(),ShapeKind.Rectangle,10,20,30,40)]}));
    [Theory][InlineData(0)][InlineData(-430)]
    public void AbsoluteKeepsDrawingPlacementRegardlessOfHead(double minimum)
    {
        var p=Profile;var travel=new MachineTravel(minimum,minimum+430,minimum,minimum+430);
        var a=JobPlacement.Resolve(Plan,p,travel,new(1,1),null,new(StartFromMode.AbsoluteCoordinates));
        var b=JobPlacement.Resolve(Plan,p,travel,new(99,99),null,new(StartFromMode.AbsoluteCoordinates,JobOriginAnchor.TopRight));
        Assert.Equal(a,b);Assert.Equal(new BoundsMm(minimum+10,minimum+370,30,40),a.MachineBounds);
    }
    [Fact]public void EveryAnchorAndDeviceOriginUsesSameReferenceForFrameAndCut()
    {
        foreach(var origin in Enum.GetValues<MachineOrigin>())foreach(var anchor in Enum.GetValues<JobOriginAnchor>())
        {
            var p=Profile with {Origin=origin};var target=new PointMm(200,200);var travel=new MachineTravel(0,430,0,430);var request=new JobPlacementRequest(StartFromMode.CurrentPosition,anchor);
            var resolved=JobPlacement.Resolve(Plan,p,travel,target,null,request);var framed=JobPlacement.Resolve(JobFootprint.Frame(Plan),p,travel,target,null,request);Assert.Equal(resolved,framed);
            var i=(int)anchor;var mapped=JobPlacement.MapToBed(p,new(10+i%3*15,20+i/3*20));Assert.Equal(target,new PointMm(mapped.X+resolved.Offset.X,mapped.Y+resolved.Offset.Y));
        }
    }
    [Fact]public void SavedOriginIsIndependentOfCurrentHeadAndMissingOrOverflowRejects()
    {
        var t=new MachineTravel(0,430,0,430);var r=new JobPlacementRequest(StartFromMode.UserOrigin);var saved=new PointMm(100,100);
        Assert.Equal(JobPlacement.Resolve(Plan,Profile,t,new(1,1),saved,r),JobPlacement.Resolve(Plan,Profile,t,new(300,300),saved,r));
        Assert.Throws<InvalidDataException>(()=>JobPlacement.Resolve(Plan,Profile,t,new(1,1),null,r));
        Assert.Throws<InvalidDataException>(()=>JobPlacement.Resolve(Plan,Profile,t,new(420,420),null,new()));
    }
    [Fact]public void ExactBedTargetUsesResolvedMachineZeroAndRejectsOutsideBed()
    {
        var t=new MachineTravel(-430,0,-430,0);Assert.Equal(new PointMm(-420,-410),JobPlacement.BedTarget(Profile,t,new(10,20)));
        Assert.Throws<InvalidDataException>(()=>JobPlacement.BedTarget(Profile,t,new(431,20)));
    }
}
