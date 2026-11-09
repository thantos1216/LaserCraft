using LaserCraft.Domain;

namespace LaserCraft.IntegrationTests;

public sealed class SvgCurveFileTests
{
    [Fact]
    public void CurveFixtureImportsPlansFillsExportsAndSavesEditablePolylines()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "samples", "bezier-curves.svg")); var shapes = SvgImport.Read(text, curveTolerance: .025);
        Assert.Equal(3, shapes.Count); Assert.All(shapes, s => Assert.True(s.Path!.Contours.Sum(c => c.Points.Length) > 10));
        Assert.Equal(1, shapes.Count(s => s.Path!.Contours[0].Closed)); var document = DesignDocument.Empty() with { Shapes = shapes };
        var plan = ToolpathPlanner.Build(document); Assert.True(plan.CutLength > 500); Assert.EndsWith("M5\nM9\n", GrblExport.Write(plan, DeviceProfile.Simulator()));
        var filled = document with { Shapes = [shapes[2]], Layers = [document.Layers[0] with { Mode = CutMode.Fill, Interval = 1 }] }; Assert.True(ToolpathPlanner.Build(filled).CutLength > 0);
        var reimported = SvgImport.Read(SvgExport.Write(document)); Assert.Equal(shapes.Select(s => s.Path!.Contours[0].Closed), reimported.Select(s => s.Path!.Contours[0].Closed)); Assert.All(reimported, s => Assert.Equal(ShapeKind.Path, s.Kind));
        var file = Path.GetTempFileName();
        try { ProjectFile.Save(file, document); var loaded = ProjectFile.Load(file); Assert.Equal(plan.CutLength, ToolpathPlanner.Build(loaded).CutLength, 8); Assert.Equal(shapes[0].Path!.Contours[0].Points.ToArray(), loaded.Shapes[0].Path!.Contours[0].Points.ToArray()); }
        finally { File.Delete(file); }
    }
}
