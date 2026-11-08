using System.Collections.Immutable;

namespace LaserCraft.Domain;

/// <summary>Edits world-space polyline nodes, retaining object identity and layer.</summary>
public static class PathNodeEditing
{
    private static ImmutableArray<PathContour> Checked(DesignShape shape, int contour, int vertex)
    {
        if (shape.Kind != ShapeKind.Path) throw new InvalidDataException("Select a path node first.");
        VectorPaths.Validate(shape);
        var paths = VectorPaths.World(shape);
        if (contour < 0 || contour >= paths.Length || vertex < 0 || vertex >= paths[contour].Points.Length)
            throw new InvalidDataException("The selected node is no longer available.");
        return paths;
    }
    public static DesignShape InsertAfter(DesignShape shape, int contour, int vertex, PointMm? point = null)
    {
        var paths = Checked(shape, contour, vertex); var c = paths[contour];
        if (!c.Closed && vertex == c.Points.Length - 1) throw new InvalidDataException("The last node has no following segment. Select another node.");
        var a = c.Points[vertex]; var b = c.Points[(vertex + 1) % c.Points.Length];
        var p = point ?? new PointMm((a.X + b.X) / 2, (a.Y + b.Y) / 2);
        return VectorPaths.Create(paths.SetItem(contour, c with { Points = c.Points.Insert(vertex + 1, p) }), shape.Layer, shape.Id);
    }
    public static DesignShape Remove(DesignShape shape, int contour, int vertex)
    {
        var paths = Checked(shape, contour, vertex); var c = paths[contour];
        if (c.Points.Length <= (c.Closed ? 3 : 2)) throw new InvalidDataException("A closed contour needs three nodes; an open contour needs two. Remove the contour or object instead.");
        return VectorPaths.Create(paths.SetItem(contour, c with { Points = c.Points.RemoveAt(vertex) }), shape.Layer, shape.Id);
    }
    public static DesignShape RemoveContour(DesignShape shape, int contour, int vertex)
    {
        var paths = Checked(shape, contour, vertex);
        if (paths.Length == 1) throw new InvalidDataException("This is the last contour. Use Edit / Delete to remove the object.");
        return VectorPaths.Create(paths.RemoveAt(contour), shape.Layer, shape.Id);
    }
    public static DesignShape Break(DesignShape shape, int contour, int vertex)
    {
        var paths = Checked(shape, contour, vertex); var c = paths[contour];
        if (c.Closed)
        {
            // Duplicate the break point as two independent open endpoints: no segment is lost.
            var points = Enumerable.Range(0, c.Points.Length + 1).Select(i => c.Points[(vertex + i) % c.Points.Length]).ToImmutableArray();
            paths = paths.SetItem(contour, new(points));
        }
        else
        {
            if (vertex == 0 || vertex == c.Points.Length - 1) throw new InvalidDataException("Select an interior node to split an open contour.");
            paths = paths.SetItem(contour, new(c.Points.Take(vertex + 1).ToImmutableArray()))
                .Insert(contour + 1, new(c.Points.Skip(vertex).ToImmutableArray()));
        }
        return VectorPaths.Create(paths, shape.Layer, shape.Id);
    }
}
