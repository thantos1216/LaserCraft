using LaserCraft.Domain;

namespace LaserCraft.UnitTests;

public sealed class SvgCurveTests
{
    private static PointMm Cubic(PointMm a, PointMm b, PointMm c, PointMm d, double t)
    { var u = 1 - t; return new(u * u * u * a.X + 3 * u * u * t * b.X + 3 * u * t * t * c.X + t * t * t * d.X, u * u * u * a.Y + 3 * u * u * t * b.Y + 3 * u * t * t * c.Y + t * t * t * d.Y); }
    private static void SameGeometry(DesignShape a, DesignShape b)
    {
        var x = VectorPaths.World(a); var y = VectorPaths.World(b); Assert.Equal(x.Length, y.Length);
        for (int i = 0; i < x.Length; i++) { Assert.Equal(x[i].Closed, y[i].Closed); Assert.Equal(x[i].Points.Length, y[i].Points.Length); for (int j = 0; j < x[i].Points.Length; j++) { Assert.Equal(x[i].Points[j].X, y[i].Points[j].X, 8); Assert.Equal(x[i].Points[j].Y, y[i].Points[j].Y, 8); } }
    }
    [Fact]
    public void CubicSamplesStayWithinThePhysicalTolerance()
    {
        var a = new PointMm(10, 20); var b = new PointMm(10, 120); var c = new PointMm(110, 120); var d = new PointMm(110, 20);
        var curve = SvgPaths.Read("M10 20 C10 120 110 120 110 20", curveTolerance: .025); var edges = VectorPaths.Edges(curve).ToArray();
        Assert.True(edges.Length > 10);
        for (int i = 0; i <= 1000; i++) { var p = Cubic(a, b, c, d, i / 1000.0); Assert.InRange(edges.Min(e => VectorPaths.Distance(p, e.From, e.To)), 0, .02500001); }
        Assert.Equal(a, VectorPaths.World(curve)[0].Points[0]); Assert.Equal(d, VectorPaths.World(curve)[0].Points[^1]);
    }
    [Fact]
    public void QuadraticSamplesStayWithinToleranceAndTighterToleranceAddsDetail()
    {
        var coarse = SvgPaths.Read("M0 0 Q50 100 100 0", curveTolerance: 1); var fine = SvgPaths.Read("M0 0 Q50 100 100 0", curveTolerance: .01);
        Assert.True(fine.Path!.Contours[0].Points.Length > coarse.Path!.Contours[0].Points.Length); var edges = VectorPaths.Edges(fine).ToArray();
        for (int i = 0; i <= 1000; i++) { var t = i / 1000.0; var p = new PointMm(100 * t, 200 * (1 - t) * t); Assert.InRange(edges.Min(e => VectorPaths.Distance(p, e.From, e.To)), 0, .01000001); }
    }
    [Theory]
    [InlineData("M10 20 c0 40 40 40 40 0 0 -40 40 -40 40 0", "M10 20 C10 60 50 60 50 20 C50 -20 90 -20 90 20")]
    [InlineData("M10 20 C10 60 50 60 50 20 s40 -40 40 0", "M10 20 C10 60 50 60 50 20 C50 -20 90 -20 90 20")]
    [InlineData("M10 20 q20 40 40 0 t40 0", "M10 20 Q30 60 50 20 Q70 -20 90 20")]
    [InlineData("M10 20 Q30 60 50 20 L60 20 T90 20", "M10 20 Q30 60 50 20 L60 20 Q60 20 90 20")]
    [InlineData("M10 20 C10 60 50 60 50 20 L60 20 S80 60 90 20", "M10 20 C10 60 50 60 50 20 L60 20 C60 20 80 60 90 20")]
    [InlineData("M10 20 Q30 60 50 20 C50 30 60 30 60 20 T90 20", "M10 20 Q30 60 50 20 C50 30 60 30 60 20 Q60 20 90 20")]
    [InlineData("M10 20 S30 60 50 20", "M10 20 C10 20 30 60 50 20")]
    [InlineData("M10 20 T50 20", "M10 20 Q10 20 50 20")]
    public void RelativeRepeatedAndSmoothCommandsMatchExpandedGeometry(string compact, string expanded) => SameGeometry(SvgPaths.Read(compact), SvgPaths.Read(expanded));
    [Fact]
    public void CollinearOvershootAndCoincidentEndpointLoopsAreNotFlattenedAway()
    {
        var backtrack = SvgPaths.Read("M100 100 C200 100 0 100 110 100"); var d = DesignDocument.Empty() with { Shapes = [backtrack] };
        Assert.True(ToolpathPlanner.Build(d).CutLength > 100); Assert.True(backtrack.Path!.Contours[0].Points.Length > 2);
        var loop = SvgPaths.Read("M50 50 C0 100 100 100 50 50"); Assert.True(loop.Height > 30); Assert.True(loop.Width > 20); Assert.False(loop.Path!.Contours[0].Closed);
    }
    [Fact]
    public void PhysicalToleranceIsAppliedAfterViewboxMapping()
    {
        var a = SvgImport.Read("<svg width='100mm' height='100mm' viewBox='0 0 1000 1000'><path d='M100 200 C100 800 900 800 900 200'/></svg>").Single();
        var b = SvgImport.Read("<svg width='100mm' height='100mm' viewBox='0 0 100 100'><path d='M10 20 C10 80 90 80 90 20'/></svg>").Single();
        SameGeometry(a, b);
    }
    [Fact]
    public void CloseAndMovetoResetSmoothControlHistory()
    {
        SameGeometry(SvgPaths.Read("M10 10 C20 20 30 20 40 10 L40 40Z S20 0 40 10"), SvgPaths.Read("M10 10 C20 20 30 20 40 10 L40 40Z C10 10 20 0 40 10"));
        SameGeometry(SvgPaths.Read("M10 10 Q20 20 30 10 M40 10 T60 10"), SvgPaths.Read("M10 10 Q20 20 30 10 M40 10 Q40 10 60 10"));
    }
    [Theory]
    [InlineData("M0 0 C1 1 2 2")]
    [InlineData("M0 0 Q1 1")]
    [InlineData("M0 0 S1 1")]
    [InlineData("M0 0 T1")]
    [InlineData("M0 0 A1 1 0 0 0 2 2")]
    [InlineData("M0 0 C1e999 1 2 2 3 3")]
    [InlineData("M0 0 C1e308 0 -1e308 0 1 0")]
    public void UnsupportedMalformedAndUnboundedCurveWorkIsRejected(string data) => Assert.Throws<InvalidDataException>(() => SvgPaths.Read(data));
    [Fact]
    public void AggregateCurveExpansionStopsAtTheProjectBudget()
    {
        var data = "M0 0 " + string.Concat(Enumerable.Repeat("C0 100 100 100 100 0 C100 -100 0 -100 0 0 ", 30));
        var svg = "<svg width='400mm' height='400mm' viewBox='0 0 400 400'>" + string.Concat(Enumerable.Repeat("<path d='" + data + "'/>", 50)) + "</svg>";
        Assert.Contains("project", Assert.Throws<InvalidDataException>(() => SvgImport.Read(svg, curveTolerance: .001)).Message);
    }
    [Fact]
    public void InvalidToleranceCancellationAndLateUnsupportedElementsFailExplicitly()
    {
        foreach (var tolerance in new[] { .0009, 1.01, double.NaN }) Assert.Throws<InvalidDataException>(() => SvgImport.Read("<svg/>", curveTolerance: tolerance));
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); Assert.Throws<OperationCanceledException>(() => SvgImport.Read("<svg/>", cancellationToken: cancel.Token));
        Assert.Throws<InvalidDataException>(() => SvgImport.Read("<svg><path d='M0 0 Q10 20 20 0'/><path d='M0 0 A1 1 0 0 0 2 2'/></svg>"));
    }
}
