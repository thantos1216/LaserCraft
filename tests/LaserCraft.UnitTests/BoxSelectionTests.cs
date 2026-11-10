using LaserCraft.Domain;
namespace LaserCraft.UnitTests;
public sealed class BoxSelectionTests
{
    private static DesignShape Rect(double x, double y = 10) => new(Guid.NewGuid(), ShapeKind.Rectangle, x, y, 10, 10);
    [Fact] public void WindowContainsWhileCrossingOverlapsIncludingBoundary()
    {
        var a = Rect(10); var b = Rect(30); var d = DesignDocument.Empty() with { Shapes = [a, b] };
        Assert.Equal(new[] { a.Id }, BoxSelection.Select(d, new HashSet<Guid>(), new(0, 0, 25, 25), false));
        Assert.Empty(BoxSelection.Select(d, new HashSet<Guid>(), new(15, 15, 2, 2), false));
        Assert.Contains(a.Id, BoxSelection.Select(d, new HashSet<Guid>(), new(15, 15, 2, 2), true));
        Assert.Contains(a.Id, BoxSelection.Select(d, new HashSet<Guid>(), new(20, 20, 1, 1), true));
    }
    [Fact] public void GroupsUseWholeBoundsForWindowAndVisibleMemberForCrossing()
    {
        var a = Rect(10) with { GroupId = Guid.NewGuid() }; var b = Rect(40) with { GroupId = a.GroupId, Layer = 1 };
        var d = DesignDocument.Empty() with { Shapes = [a, b] }; d.Layers.Add(new("Hidden", "#123456", Visible: false));
        Assert.Empty(BoxSelection.Select(d, new HashSet<Guid>(), new(0, 0, 25, 25), false));
        Assert.Equal(2, BoxSelection.Select(d, new HashSet<Guid>(), new(0, 0, 55, 25), false).Count);
        Assert.Equal(2, BoxSelection.Select(d, new HashSet<Guid>(), new(15, 15, 1, 1), true).Count);
        Assert.Empty(BoxSelection.Select(d, new HashSet<Guid>(), new(45, 15, 1, 1), true));
        Assert.Single(BoxSelection.Select(d, new HashSet<Guid>(), new(0, 0, 25, 25), false, individual: true));
    }
    [Fact] public void AdditiveKeepsValidSelectionAndHiddenStandaloneIsExcluded()
    {
        var a = Rect(10); var b = Rect(30); var h = Rect(50) with { Layer = 1 }; var d = DesignDocument.Empty() with { Shapes = [a, b, h] }; d.Layers.Add(new("Hidden", "#123456", Visible: false));
        var result = BoxSelection.Select(d, new HashSet<Guid> { b.Id, Guid.NewGuid() }, new(0, 0, 25, 25), false, true);
        Assert.True(result.SetEquals(new[] { a.Id, b.Id })); Assert.Equal(2, BoxSelection.Select(d, new HashSet<Guid>(), new(0, 0, 100, 100), false).Count);
    }
    [Fact] public void BoundsSupportDegeneratePathsAndRejectInvalidBoxes()
    {
        var line = VectorPaths.Create([new([new(10,10),new(20,10)])]); var d = DesignDocument.Empty() with { Shapes = [line] };
        Assert.Contains(line.Id, BoxSelection.Select(d, new HashSet<Guid>(), new(10, 10, 10, 0), false));
        foreach (var box in new[] { new BoundsMm(double.NaN,0,1,1), new(0,0,-1,1), new(double.MaxValue,0,double.MaxValue,1) }) Assert.Throws<InvalidDataException>(() => BoxSelection.Select(d, new HashSet<Guid>(), box, true));
    }
}
