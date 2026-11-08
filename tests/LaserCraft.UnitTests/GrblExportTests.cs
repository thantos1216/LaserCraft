using System.Globalization;
using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class GrblExportTests
{
    private static DeviceProfile Profile(MachineOrigin origin = MachineOrigin.TopLeft) => DeviceProfile.Simulator() with { BedWidth = 100, BedHeight = 80, Origin = origin, AirAssistEnabled = true };
    private static ToolpathPlan Plan() => new(new ToolpathSegment[] {
        new(new(0, 0), new(10, 20), 0, true, 6000, false, 0),
        new(new(10, 20), new(30, 20), 0, false, 1200, true, 25),
        new(new(30, 20), new(40, 20), 1, true, 900, false, 90),
        new(new(40, 20), new(50, 20), 1, false, 900, false, 12.5) });
    [Fact]
    public void ExportPreservesFeedPowerOrderAndOffTravel()
    {
        var code = GrblExport.Write(Plan(), Profile());
        Assert.StartsWith("; LaserCraft", code); Assert.Contains("G21\nG90\nG94\nM5\nM9\n", code);
        Assert.Contains("M4 S0\n", code); Assert.DoesNotContain("M3", code);
        Assert.Contains("M8\nG1 X30 Y20 F1200 S250\n; Layer 01\nM9\nG1 X40 Y20 F900 S0", code);
        Assert.Contains("G1 X50 Y20 F900 S125", code); Assert.EndsWith("M5\nM9\n", code);
        Assert.DoesNotContain("$32=1\n", code); // Explanatory comment only, never changes firmware settings.
    }
    [Theory]
    [InlineData(MachineOrigin.TopLeft, "X10 Y20")]
    [InlineData(MachineOrigin.TopRight, "X90 Y20")]
    [InlineData(MachineOrigin.BottomLeft, "X10 Y60")]
    [InlineData(MachineOrigin.BottomRight, "X90 Y60")]
    public void MapsAllBedOrigins(MachineOrigin origin, string expected) => Assert.Contains("G1 " + expected + " F6000 S0", GrblExport.Write(Plan(), Profile(origin)));
    [Fact]
    public void RequiresAirCapabilityAndQualifiedFamily()
    {
        Assert.Throws<InvalidDataException>(() => GrblExport.Write(Plan(), Profile() with { AirAssistEnabled = false }));
        Assert.Throws<InvalidDataException>(() => GrblExport.Write(Plan(), Profile() with { Controller = ControllerFamily.Ruida }));
    }
    [Fact]
    public void RejectsEmptyDiscontinuousNonfiniteAndOffBedPlans()
    {
        Assert.Throws<InvalidDataException>(() => GrblExport.Write(new([]), Profile()));
        foreach (var bad in new[] { new PointMm(-1, 0), new(double.NaN, 0), new(101, 0) })
            Assert.Throws<InvalidDataException>(() => GrblExport.Write(new([new(new(0, 0), bad, 0, true, 1000)]), Profile()));
        Assert.Throws<InvalidDataException>(() => GrblExport.Write(new([Plan().Segments[0], Plan().Segments[2]]), Profile()));
        Assert.Throws<InvalidDataException>(() => GrblExport.Write(new([Plan().Segments[0] with { Speed = double.NaN }]), Profile()));
    }
    [Fact]
    public void UsesInvariantDecimalFormatting()
    {
        var prior = CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE"); var plan = new ToolpathPlan([new(new(0, 0), new(1.25, 2.5), 0, false, 123.45, false, 10)]); Assert.Contains("X1.25 Y2.5 F123.45 S100", GrblExport.Write(plan, Profile())); }
        finally { CultureInfo.CurrentCulture = prior; }
    }
}
