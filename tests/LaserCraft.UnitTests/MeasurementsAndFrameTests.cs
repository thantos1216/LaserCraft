using LaserCraft.Domain;
namespace LaserCraft.UnitTests;
public sealed class MeasurementsAndFrameTests
{
    [Theory][InlineData(1,25.4)][InlineData(.125,3.175)][InlineData(-1,-25.4)]
    public void ImperialValuesPreserveMillimetresAndRoundTrip(double input,double mm)
    {
        Assert.Equal(mm,Measurements.ToMillimetres(input,true),10);Assert.Equal(input,Measurements.ToDisplay(mm,true),10);
        Assert.Equal(mm,Measurements.ToDisplay(mm,false));Assert.Throws<InvalidDataException>(()=>Measurements.ToMillimetres(double.NaN,true));
    }
    [Fact]public void InchSquareEmitsSameMillimetreProgram()
    {
        ToolpathPlan Plan(double size)=>ToolpathPlanner.Build(DesignDocument.Empty() with {Shapes=[new(Guid.NewGuid(),ShapeKind.Rectangle,10,10,size,size)]});
        var profile=DeviceProfile.Simulator();Assert.Equal(GrblExport.Write(Plan(25.4),profile),GrblExport.Write(Plan(Measurements.ToMillimetres(1,true)),profile));
    }
    [Fact]public void CircularFrameIsClosedZeroPowerAndChecksAdditionalTravel()
    {
        var plan=JobFootprint.TrimInitialTravel(ToolpathPlanner.Build(DesignDocument.Empty() with {Shapes=[new(Guid.NewGuid(),ShapeKind.Rectangle,10,10,40,10)]}));var frame=JobFootprint.Frame(plan,circular:true);
        Assert.Equal(128,frame.Segments.Count);Assert.Equal(frame.Segments[0].From,frame.Segments[^1].To);Assert.All(frame.Segments,s=>{Assert.Equal(0,s.Power);Assert.False(s.AirAssist);});
        var bounds=JobFootprint.Measure(frame);Assert.Equal(40,bounds.Width,8);Assert.Equal(40,bounds.Height,8);
        Assert.Throws<InvalidDataException>(()=>JobPlacement.ValidateOffset(frame,DeviceProfile.Simulator() with {Origin=MachineOrigin.TopLeft},new(0,400,0,400),new(0,0)));
        var p=DeviceProfile.Simulator() with {Origin=MachineOrigin.TopLeft};var placed=JobPlacement.ValidateOffset(frame,p,new(0,400,0,400),new(100,100));Assert.Contains("G53",GrblExport.WritePlaced(frame,p,placed));
    }
}
