using System.Globalization;
using LaserCraft.Domain;
namespace LaserCraft.Devices;
public sealed partial class GrblMonitor
{
    public async Task<PointMm> PositionAsync(DeviceProfile profile,PointMm? bedTarget=null,double feed=600,bool captureOrigin=false,CancellationToken token=default)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref disposalStarted)!=0,this);
        profile.Validate();if(profile.Controller!=ControllerFamily.Grbl||profile.Connection==ConnectionKind.Simulator)throw new InvalidDataException("Positioning requires physical GRBL.");
        if(bedTarget is not null&&captureOrigin)throw new InvalidDataException("Capture and movement are separate actions.");
        if(!double.IsFinite(feed)||feed<60||feed>3000)throw new InvalidDataException("Position feed must be 60–3000 mm/min.");
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token,lifetime.Token);timeout.CancelAfter(TimeSpan.FromSeconds(10));await gate.WaitAsync(timeout.Token);
        try
        {
            RequireSynchronizedConsole();var travel=ResolveMachineTravel(profile);
            async Task<GrblConsoleResult> Exchange(string command){var reply=await ExchangeConsoleCore(GrblConsoleCommand.Prepare(command,true),timeout.Token);if(reply.Outcome is GrblConsoleOutcome.Rejected or GrblConsoleOutcome.Alarm)throw new IOException("Positioning rejected: "+reply.Lines[^1]);return reply;}
            var settings=await Exchange("$$");var units=settings.Lines.Where(l=>l.StartsWith("$13=",StringComparison.Ordinal)).Select(GrblSetting.Parse).ToArray();if(units.Length!=1||!double.TryParse(units[0].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out var unit)||unit!=0)throw new InvalidDataException("Positioning requires reported millimetres ($13=0).");
            var report=await Exchange("?");var status=GrblStatus.Parse(report.Lines.Last(l=>l.StartsWith('<')));if(status.State!="Idle"||status.Coordinates!="MPos"||status.X is not double x||status.Y is not double y)throw new InvalidDataException("Positioning requires fresh Idle/MPos.");
            var point=new PointMm(x,y);
            if(captureOrigin){userOrigin=point;return point;}
            if(bedTarget is PointMm bed)
            {
                var target=JobPlacement.BedTarget(profile,travel,bed);await Exchange("M5 S0");await Exchange(FormattableString.Invariant($"$J=G21 G90 G53 X{target.X:0.######} Y{target.Y:0.######} F{feed:0.###}"));return target;
            }
            return point;
        }
        finally{gate.Release();}
    }
}
