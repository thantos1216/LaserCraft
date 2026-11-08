using LaserCraft.Domain;

namespace LaserCraft.IntegrationTests;

public sealed class PathJoiningFileTests
{
    [Fact]
    public void PortableJoiningSamplePlansExportsAndReloadsBothJoinModes()
    {
        var source = ProjectFile.Load(Path.Combine(AppContext.BaseDirectory, "samples", "endpoint-joining.lcraft")); var ids = source.Shapes.Select(s => s.Id).ToHashSet();
        var originalLength = ToolpathPlanner.Build(source).CutLength;
        Assert.Throws<InvalidDataException>(() => PathJoining.Prepare(source, ids, .1, PathJoinMode.MergeAtMidpoint));
        foreach (var mode in Enum.GetValues<PathJoinMode>())
        {
            var result = PathJoining.Prepare(source, ids, 1, mode); Assert.InRange(result.Gap, .4472, .4473); var plan = ToolpathPlanner.Build(result.Document);
            if (mode == PathJoinMode.BridgeGap) Assert.Equal(originalLength + result.Gap, plan.CutLength, 8);
            var code = GrblExport.Write(plan, DeviceProfile.Simulator()); Assert.Contains("M4", code); Assert.EndsWith("M5\nM9\n", code);
            var svg = SvgExport.Write(result.Document); var restored = Assert.Single(SvgImport.Read(svg)); Assert.False(restored.Path!.Contours[0].Closed);
            var roundTrip = source with { Shapes = [restored] }; Assert.Equal(plan.CutLength, ToolpathPlanner.Build(roundTrip).CutLength, 5);
        }
        Assert.Equal(2, source.Shapes.Count);
    }
}
