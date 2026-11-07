namespace LaserCraft.Domain;

public enum RasterMode { Threshold, FloydSteinberg, OrderedBayer, Grayscale }
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
    public static RasterGrid ProcessRgba(int width, int height, ReadOnlySpan<byte> rgba, RasterSettings settings, CancellationToken cancellationToken = default)
    {
        settings.Validate(); if (width < 1 || height < 1 || width > 16000 || height > 16000 || (long)width * height > 4_000_000 || rgba.Length != (long)width * height * 4) throw new InvalidDataException("Invalid RGBA image or raster sample limit exceeded.");
        var tones = new float[width * height]; var output = new byte[tones.Length];
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
                if (settings.Mode != RasterMode.FloydSteinberg) continue;
                var error = tone - value;
                if (x + 1 < width) tones[i + 1] += error * 7 / 16;
                if (y + 1 < height) { if (x > 0) tones[i + width - 1] += error * 3 / 16; tones[i + width] += error * 5 / 16; if (x + 1 < width) tones[i + width + 1] += error / 16; }
            }
        }
        return new(width, height, output);
    }
}
