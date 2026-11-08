namespace LaserCraft.Domain;

public enum RasterMode { Threshold, FloydSteinberg, OrderedBayer, Grayscale, Stucki, Jarvis, Atkinson }
public sealed record RasterSettings(RasterMode Mode = RasterMode.FloydSteinberg, double Brightness = 0, double Contrast = 0, double Gamma = 1, bool Invert = false, int Threshold = 128, bool Bidirectional = true, double Overscan = 0)
{
    public void Validate()
    {
        if (!Enum.IsDefined(Mode) || !double.IsFinite(Brightness) || Brightness < -100 || Brightness > 100 || !double.IsFinite(Contrast) || Contrast < -100 || Contrast > 100 || !double.IsFinite(Gamma) || Gamma < .1 || Gamma > 5 || Threshold < 0 || Threshold > 255 || !double.IsFinite(Overscan) || Overscan < 0 || Overscan > 100)
            throw new InvalidDataException("Invalid raster processing settings.");
    }
}
public sealed record RasterGrid(int Width, int Height, byte[] Darkness)
{
    public void Validate() { if (Width < 1 || Height < 1 || Width > 16000 || Height > 16000 || (long)Width * Height > 4_000_000 || Darkness is null || Darkness.Length != (long)Width * Height) throw new InvalidDataException("Invalid raster grid or sample limits exceeded. Increase line interval."); }
}
public static class RasterProcessing
{
    private static readonly int[] Bayer = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];
    private static readonly (int X, int Y, float Weight)[] Floyd = [(1, 0, 7f / 16), (-1, 1, 3f / 16), (0, 1, 5f / 16), (1, 1, 1f / 16)];
    private static readonly (int X, int Y, float Weight)[] Stucki = [(1, 0, 8f / 42), (2, 0, 4f / 42), (-2, 1, 2f / 42), (-1, 1, 4f / 42), (0, 1, 8f / 42), (1, 1, 4f / 42), (2, 1, 2f / 42), (-2, 2, 1f / 42), (-1, 2, 2f / 42), (0, 2, 4f / 42), (1, 2, 2f / 42), (2, 2, 1f / 42)];
    private static readonly (int X, int Y, float Weight)[] Jarvis = [(1, 0, 7f / 48), (2, 0, 5f / 48), (-2, 1, 3f / 48), (-1, 1, 5f / 48), (0, 1, 7f / 48), (1, 1, 5f / 48), (2, 1, 3f / 48), (-2, 2, 1f / 48), (-1, 2, 3f / 48), (0, 2, 5f / 48), (1, 2, 3f / 48), (2, 2, 1f / 48)];
    private static readonly (int X, int Y, float Weight)[] Atkinson = [(1, 0, 1f / 8), (2, 0, 1f / 8), (-1, 1, 1f / 8), (0, 1, 1f / 8), (1, 1, 1f / 8), (0, 2, 1f / 8)];
    public static RasterGrid ProcessRgba(int width, int height, ReadOnlySpan<byte> rgba, RasterSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate(); if (width < 1 || height < 1 || width > 16000 || height > 16000 || (long)width * height > 4_000_000 || rgba.Length != (long)width * height * 4) throw new InvalidDataException("Invalid RGBA image or raster sample limit exceeded.");
        var tones = new float[width * height]; var output = new byte[tones.Length];
        var kernel = settings.Mode switch { RasterMode.FloydSteinberg => Floyd, RasterMode.Stucki => Stucki, RasterMode.Jarvis => Jarvis, RasterMode.Atkinson => Atkinson, _ => [] };
        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                var i = y * width + x; var p = i * 4; var gray = (.2126 * rgba[p] + .7152 * rgba[p + 1] + .0722 * rgba[p + 2]) / 255;
                gray = Math.Clamp((gray - .5) * (1 + settings.Contrast / 100) + .5 + settings.Brightness / 100, 0, 1);
                gray = Math.Pow(gray, 1 / settings.Gamma); if (settings.Invert) gray = 1 - gray;
                tones[i] = (float)(1 - rgba[p + 3] / 255.0 * (1 - gray));
            }
        }
        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < width; x++)
            {
                var i = y * width + x; if (rgba[i * 4 + 3] == 0) { output[i] = 0; continue; }
                var tone = tones[i];
                if (settings.Mode == RasterMode.Grayscale) { output[i] = (byte)Math.Clamp((int)Math.Round((1 - tone) * 255), 0, 255); continue; }
                var threshold = settings.Mode == RasterMode.OrderedBayer ? (Bayer[(y % 4) * 4 + x % 4] + .5) / 16 : settings.Mode == RasterMode.Threshold ? settings.Threshold / 255.0 : .5;
                var value = tone >= threshold ? 1f : 0f; output[i] = value == 0 ? (byte)255 : (byte)0;
                var error = tone - value;
                foreach (var tap in kernel) { var tx = x + tap.X; var ty = y + tap.Y; if (tx >= 0 && tx < width && ty < height) tones[ty * width + tx] += error * tap.Weight; }
            }
        }
        return new(width, height, output);
    }
}
