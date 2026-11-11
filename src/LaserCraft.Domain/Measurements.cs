namespace LaserCraft.Domain;
public static class Measurements
{
    public static double ToDisplay(double millimetres,bool inches)=>Convert(millimetres,inches?1/25.4:1);
    public static double ToMillimetres(double value,bool inches)=>Math.Round(Convert(value,inches?25.4:1),9);
    private static double Convert(double value,double factor)=>double.IsFinite(value)&&double.IsFinite(value*factor)?value*factor:throw new InvalidDataException("Measurement must be finite.");
}
