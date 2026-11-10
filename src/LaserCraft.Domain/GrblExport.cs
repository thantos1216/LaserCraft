using System.Globalization;
using System.Text;

namespace LaserCraft.Domain;

/// <summary>GRBL 1.1 laser-mode program generation for export or reviewed streaming. Never opens a transport.</summary>
public static class GrblExport
{
    public static string Write(ToolpathPlan plan, DeviceProfile profile, PointMm? machineAnchor = null)
    {
        profile.Validate();
        if (profile.Controller != ControllerFamily.Grbl) throw new InvalidDataException("Export currently supports GRBL 1.1 only.");
        if (plan.Segments.Count == 0 || plan.Segments.Count > 200000) throw new InvalidDataException("Export requires 1..200000 planned segments.");
        PointMm? previous = null;
        foreach (var segment in plan.Segments)
        {
            ValidatePoint(segment.From); ValidatePoint(segment.To);
            if (!double.IsFinite(segment.Speed) || segment.Speed < .001 || segment.Speed > 1000000 || !double.IsFinite(segment.Power) || segment.Power < 0 || segment.Power > 100 || segment.Layer < 0)
                throw new InvalidDataException("Invalid segment feed, power or layer.");
            if (previous is not null && previous.Value != segment.From) throw new InvalidDataException("Toolpath is discontinuous.");
            if (segment.AirAssist && !profile.AirAssistEnabled) throw new InvalidDataException("A layer requests air assist; enable the configured controller air output in its device profile or disable the layer request.");
            previous = segment.To;
        }
        if (machineAnchor is PointMm anchor && (!double.IsFinite(anchor.X) || !double.IsFinite(anchor.Y) || Math.Abs(anchor.X) > 10000 || Math.Abs(anchor.Y) > 10000)) throw new InvalidDataException("Invalid machine anchor.");
        var mapped = plan.Segments.SelectMany(s => new[] { MapBase(s.From), MapBase(s.To) });
        var minX = mapped.Min(p => p.X); var minY = mapped.Min(p => p.Y);
        var prefix = machineAnchor is null ? "" : "G53 ";
        var text = new StringBuilder("; LaserCraft offline GRBL 1.1 export\n; Requires laser mode $32=1 and matching $30 / S-value maximum\n; Absolute work coordinates; set work zero to the configured bed origin\nG21\nG90\nG94\nM5\nM9\n");
        var start = Map(plan.Segments[0].From);
        text.AppendLine($"{prefix}G0 X{N(start.X)} Y{N(start.Y)} S0");
        text.AppendLine("M4 S0");
        var air = false; var layer = -1;
        foreach (var segment in plan.Segments)
        {
            if (layer != segment.Layer) { layer = segment.Layer; text.AppendLine($"; Layer {layer:00}"); }
            var requestedAir = !segment.Travel && segment.Power > 0 && segment.AirAssist;
            if (air != requestedAir) { air = requestedAir; text.AppendLine(air ? profile.AirOutput.ToString() : "M9"); }
            var target = Map(segment.To);
            var power = segment.Travel ? 0 : (int)Math.Round(profile.MaxPower * segment.Power / 100, MidpointRounding.AwayFromZero);
            // Feed-controlled off-power spans retain scan/overscan speed rather than becoming rapids.
            text.AppendLine($"{prefix}G1 X{N(target.X)} Y{N(target.Y)} F{N(segment.Speed)} S{power.ToString(CultureInfo.InvariantCulture)}");
        }
        text.AppendLine("M5"); text.AppendLine("M9");
        var result = text.ToString().Replace("\r\n", "\n");
        return machineAnchor is null ? result : result.Replace("; Absolute work coordinates; set work zero to the configured bed origin", "; Machine coordinates; current head anchors mapped minimum X/Y; work offsets unchanged");

        void ValidatePoint(PointMm p)
        {
            if (!double.IsFinite(p.X) || !double.IsFinite(p.Y) || p.X < 0 || p.Y < 0 || p.X > profile.BedWidth || p.Y > profile.BedHeight)
                throw new InvalidDataException("Output exceeds the selected device bed.");
        }
        PointMm Map(PointMm p) { var mapped = MapBase(p); return machineAnchor is PointMm anchor ? new(anchor.X + mapped.X - minX, anchor.Y + mapped.Y - minY) : mapped; }
        PointMm MapBase(PointMm p) => new(
            profile.Origin is MachineOrigin.TopRight or MachineOrigin.BottomRight ? profile.BedWidth - p.X : p.X,
            profile.Origin is MachineOrigin.BottomLeft or MachineOrigin.BottomRight ? profile.BedHeight - p.Y : p.Y);
        static string N(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    }
}
