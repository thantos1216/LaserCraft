namespace LaserCraft.Domain;

public sealed record MachineTravel(double MinX, double MaxX, double MinY, double MaxY)
{
    public static MachineTravel Resolve(DeviceProfile profile, PointMm? homedMachinePosition)
    {
        profile.Validate();
        double Minimum(double travel, double? home)
        {
            if(profile.CoordinateRange==MachineCoordinateRange.Positive)return 0;
            if(profile.CoordinateRange==MachineCoordinateRange.Negative)return -travel;
            if(home is null||!double.IsFinite(home.Value)||home.Value==0)throw new InvalidDataException("Homing did not establish a signed machine range. Select Positive or Negative machine coordinates in Devices after verifying the machine.");
            return home.Value>0?0:-travel;
        }
        var x=Minimum(profile.BedWidth,homedMachinePosition?.X);var y=Minimum(profile.BedHeight,homedMachinePosition?.Y);
        return new(x,x+profile.BedWidth,y,y+profile.BedHeight);
    }
    public bool Contains(PointMm anchor, BoundsMm footprint) => double.IsFinite(anchor.X)&&double.IsFinite(anchor.Y)&&anchor.X>=MinX&&anchor.Y>=MinY&&anchor.X+footprint.Width<=MaxX&&anchor.Y+footprint.Height<=MaxY;
}
