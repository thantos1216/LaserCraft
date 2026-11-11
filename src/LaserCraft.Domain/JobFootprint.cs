namespace LaserCraft.Domain;

public static class JobFootprint
{
    public static ToolpathPlan TrimInitialTravel(ToolpathPlan plan) => new(plan.Segments.SkipWhile(s => s.Travel).ToArray());
    public static BoundsMm Measure(ToolpathPlan plan)
    {
        if (plan.Segments.Count == 0) throw new InvalidDataException("No enabled artwork to output.");
        var points = plan.Segments.SelectMany(s => new[] { s.From, s.To }).ToArray();
        if (points.Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y))) throw new InvalidDataException("Invalid job coordinates.");
        var x = points.Min(p => p.X); var y = points.Min(p => p.Y);
        return new(x, y, points.Max(p => p.X) - x, points.Max(p => p.Y) - y);
    }
    public static ToolpathPlan Frame(ToolpathPlan plan, double feed = 600,bool circular=false)
    {
        if (!double.IsFinite(feed) || feed < 60 || feed > 3000) throw new InvalidDataException("Frame feed must be 60–3000 mm/min.");
        var b = Measure(plan); if (b.Width == 0 && b.Height == 0) throw new InvalidDataException("Frame needs nonzero artwork extent.");
        if(circular)
        {
            var radius=Math.Max(b.Width,b.Height)/2;var cx=b.X+b.Width/2;var cy=b.Y+b.Height/2;
            var points=Enumerable.Range(0,129).Select(i=>new PointMm(cx+radius*Math.Cos(i%128*Math.Tau/128),cy+radius*Math.Sin(i%128*Math.Tau/128))).ToArray();
            return new(Enumerable.Range(0,128).Select(i=>new ToolpathSegment(points[i],points[i+1],0,false,feed,false,0)).ToArray());
        }
        PointMm[] corners = [new(b.X,b.Y),new(b.X+b.Width,b.Y),new(b.X+b.Width,b.Y+b.Height),new(b.X,b.Y+b.Height),new(b.X,b.Y)];
        return new(Enumerable.Range(0,4).Select(i => new ToolpathSegment(corners[i],corners[i+1],0,false,feed,false,0)).ToArray());
    }
}
