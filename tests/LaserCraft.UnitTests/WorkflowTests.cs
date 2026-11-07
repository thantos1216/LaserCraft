using LaserCraft.Domain;
using System.Text.Json;

namespace LaserCraft.UnitTests;

public sealed class WorkflowTests
{
    private static DesignDocument Shape(double x = 10, double y = 20, double w = 30, double h = 40) => DesignDocument.Empty() with { Shapes = [new(Guid.NewGuid(), ShapeKind.Rectangle, x, y, w, h)] };
    [Fact] public void NewDocumentsStartWithOneExplicitLayer() { var d = DesignDocument.Empty(); Assert.Single(d.Layers); Assert.Equal(LayerKind.Vector, d.Layers[0].Kind); d.Validate(); }
    [Fact]
    public void SvgRoundTripPreservesMillimetres()
    {
        var d = Shape(); var imported = SvgImport.Read(SvgExport.Write(d)); var a = d.Shapes[0]; var b = Assert.Single(imported); Assert.Equal(a.X, b.X, 8); Assert.Equal(a.Height, b.Height, 8);
    }
    [Fact]
    public void SvgPixelsConvertAt96Dpi()
    {
        var shapes = SvgImport.Read("<svg width='96px' height='96px' viewBox='0 0 96 96'><rect width='96' height='96'/></svg>"); Assert.Equal(25.4, shapes[0].Width, 8);
    }
    [Theory]
    [InlineData("<path d='M0 0 L1 1'/>")]
    [InlineData("<g transform='scale(2)'><rect width='4' height='4'/></g>")]
    public void UnsupportedSvgCannotSilentlyDropGeometry(string element) => Assert.Throws<InvalidDataException>(() => SvgImport.Read($"<svg>{element}</svg>"));
    [Fact] public void SvgExternalEntitiesAreRejected() => Assert.Throws<System.Xml.XmlException>(() => SvgImport.Read("<!DOCTYPE svg [<!ENTITY e SYSTEM 'file:///test'>]><svg>&e;</svg>"));
    [Fact]
    public void RectangleCutLengthAndPassCountAreCorrect()
    {
        var d = Shape(); d.Layers[0] = d.Layers[0] with { Passes = 2 }; var plan = ToolpathPlanner.Build(d); Assert.Equal(280, plan.CutLength, 8); Assert.Equal(8, plan.Segments.Count(s => !s.Travel));
    }
    [Fact]
    public void DisabledOutputIsSkippedIndependentlyOfVisibility()
    {
        var d = Shape(); d.Layers[0] = d.Layers[0] with { Visible = false }; Assert.True(ToolpathPlanner.Build(d).CutLength > 0); d.Layers[0] = d.Layers[0] with { Output = false }; Assert.Empty(ToolpathPlanner.Build(d).Segments);
    }
    [Fact] public void OffBedOutputIsRejected() => Assert.Throws<InvalidDataException>(() => ToolpathPlanner.Build(Shape(-1)));
    [Fact]
    public void SelectedOnlyPlanningIncludesExactlyTheRequestedObjects()
    {
        var d = Shape(); var id = d.Shapes[0].Id; d.Shapes.Add(new(Guid.NewGuid(), ShapeKind.Rectangle, 100, 100, 50, 50));
        Assert.Equal(140, ToolpathPlanner.Build(d, new HashSet<Guid> { id }).CutLength, 8);
        Assert.Empty(ToolpathPlanner.Build(d, new HashSet<Guid>()).Segments);
    }
    [Fact]
    public void FillUsesEvenOddHoleRule()
    {
        var d = Shape(0, 0, 10, 10); d.Shapes.Add(new(Guid.NewGuid(), ShapeKind.Rectangle, 3, 3, 4, 4)); d.Layers[0] = d.Layers[0] with { Mode = CutMode.Fill, Interval = 1 };
        var cuts = ToolpathPlanner.Build(d).Segments.Where(s => !s.Travel && s.From.Y == 3.5).ToArray(); Assert.Equal(2, cuts.Length); Assert.DoesNotContain(cuts, s => Math.Min(s.From.X, s.To.X) < 5 && Math.Max(s.From.X, s.To.X) > 5);
    }
    [Fact]
    public void SimulationPauseStopAndCompletionAreDeterministic()
    {
        var sim = new JobSimulation(ToolpathPlanner.Build(Shape())); sim.Start(); sim.Advance(1); sim.Pause(); sim.Advance(50); Assert.Equal(1, sim.Elapsed); sim.Start(); sim.Advance(10000); Assert.Equal(SimulationState.Completed, sim.State); sim.Stop(); Assert.Equal(0, sim.Elapsed); Assert.Equal(SimulationState.Stopped, sim.State);
    }
    [Fact]
    public void AlignAndMirrorPreserveObjectIds()
    {
        var d = Shape(); d.Shapes.Add(new(Guid.NewGuid(), ShapeKind.Ellipse, 60, 40, 10, 10)); var ids = d.Shapes.Select(s => s.Id).ToHashSet(); var aligned = ShapeEditing.Align(d, ids, "Left"); Assert.All(aligned.Shapes, s => Assert.Equal(10, s.X)); var mirrored = ShapeEditing.Mirror(d, ids, true); Assert.Equal(40, mirrored.Shapes[0].X); Assert.Equal(ids, mirrored.Shapes.Select(s => s.Id).ToHashSet());
    }
    [Fact]
    public void LegacyLayerSettingsReceiveCompatibleDefaults()
    {
        var layer = JsonSerializer.Deserialize<CutLayer>("{\"Name\":\"Legacy\",\"Color\":\"#112233\",\"Speed\":1200,\"Power\":20,\"Output\":true}")!;
        Assert.True(layer.Visible); Assert.Equal(1, layer.Passes); Assert.Equal(.1, layer.Interval); Assert.Equal(CutMode.Line, layer.Mode);
    }
    [Fact] public void InvalidPassCountIsRejected() { var d = Shape(); d.Layers[0] = d.Layers[0] with { Passes = 0 }; Assert.Throws<InvalidDataException>(d.Validate); }
}
