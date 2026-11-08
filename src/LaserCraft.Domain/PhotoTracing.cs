namespace LaserCraft.Domain;

public sealed record TraceTuning(int Cutoff = 0, int Threshold = 128, double NoiseSmoothing = 0, double Smoothness = 0, double Optimize = 0, bool Sketch = false, int LocalRadius = 8, double LocalContrast = 8)
{
    public void Validate()
    {
        if (Cutoff < 0 || Threshold > 255 || Threshold < 0 || Cutoff > 255 || (!Sketch && Cutoff > Threshold) || !double.IsFinite(NoiseSmoothing) || NoiseSmoothing < 0 || NoiseSmoothing > 3 || !double.IsFinite(Smoothness) || Smoothness < 0 || Smoothness > 2 || !double.IsFinite(Optimize) || Optimize < 0 || Optimize > 2 || LocalRadius < 2 || LocalRadius > 32 || !double.IsFinite(LocalContrast) || LocalContrast < 0 || LocalContrast > 64)
            throw new InvalidDataException("Invalid trace tuning. Cutoff must not exceed Threshold; smoothing and optimize values must be within their ranges.");
    }
}
public sealed record PhotoTraceResult(TraceResult Trace, RasterGrid Grayscale, RasterGrid Mask, int OriginalVertices, string RefinementNote);

public static class PhotoTracing
{
    public static PhotoTraceResult Build(RasterGrid gray, DesignShape source, TraceTuning tuning, int minimumArea = 2, CancellationToken cancellationToken = default)
    {
        var (processed, mask) = Prepare(gray, tuning, cancellationToken);
        var trace = ImageTracing.Trace(mask, source, minimumArea, cancellationToken);
        var count = trace.Path?.Path?.Contours.Sum(c => c.Points.Length) ?? 0;
        var refined = TraceRefinement.Refine(trace, tuning.Smoothness, tuning.Optimize, cancellationToken);
        return new(refined.Trace, processed, mask, count, refined.Note);
    }
    public static (RasterGrid Grayscale, RasterGrid Mask) Prepare(RasterGrid gray, TraceTuning tuning, CancellationToken cancellationToken = default)
    {
        gray.Validate(); tuning.Validate();
        if (gray.Width > 1024 || gray.Height > 1024) throw new InvalidDataException("Photo tracing is limited to 1024 samples per axis.");
        cancellationToken.ThrowIfCancellationRequested();
        var w = gray.Width; var h = gray.Height; var tones = gray.Darkness.Select(d => 255.0 - d).ToArray();
        if (tuning.NoiseSmoothing > 0)
        {
            var radius = (int)Math.Ceiling(tuning.NoiseSmoothing * 3); var weights = Enumerable.Range(-radius, radius * 2 + 1).Select(i => Math.Exp(-i * i / (2 * tuning.NoiseSmoothing * tuning.NoiseSmoothing))).ToArray();
            var sum = weights.Sum(); for (int i = 0; i < weights.Length; i++) weights[i] /= sum;
            var horizontal = new double[tones.Length]; var blurred = new double[tones.Length];
            for (int y = 0; y < h; y++) { cancellationToken.ThrowIfCancellationRequested(); for (int x = 0; x < w; x++) for (int k = -radius; k <= radius; k++) horizontal[y * w + x] += tones[y * w + Math.Clamp(x + k, 0, w - 1)] * weights[k + radius]; }
            for (int y = 0; y < h; y++) { cancellationToken.ThrowIfCancellationRequested(); for (int x = 0; x < w; x++) for (int k = -radius; k <= radius; k++) blurred[y * w + x] += horizontal[Math.Clamp(y + k, 0, h - 1) * w + x] * weights[k + radius]; }
            tones = blurred;
        }
        double[]? integral = null;
        if (tuning.Sketch)
        {
            integral = new double[(w + 1) * (h + 1)];
            for (int y = 0; y < h; y++) { cancellationToken.ThrowIfCancellationRequested(); double row = 0; for (int x = 0; x < w; x++) { row += tones[y * w + x]; integral[(y + 1) * (w + 1) + x + 1] = integral[y * (w + 1) + x + 1] + row; } }
        }
        var mask = new byte[tones.Length]; var display = new byte[tones.Length];
        for (int y = 0; y < h; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            for (int x = 0; x < w; x++)
            {
                var i = y * w + x; var tone = tones[i]; display[i] = (byte)Math.Clamp((int)Math.Round(255 - tone), 0, 255);
                var on = tone >= tuning.Cutoff && tone < tuning.Threshold;
                if (integral is not null)
                {
                    var x0 = Math.Max(0, x - tuning.LocalRadius); var x1 = Math.Min(w, x + tuning.LocalRadius + 1); var y0 = Math.Max(0, y - tuning.LocalRadius); var y1 = Math.Min(h, y + tuning.LocalRadius + 1);
                    var total = integral[y1 * (w + 1) + x1] - integral[y0 * (w + 1) + x1] - integral[y1 * (w + 1) + x0] + integral[y0 * (w + 1) + x0];
                    on = tone < total / ((x1 - x0) * (y1 - y0)) - tuning.LocalContrast;
                }
                mask[i] = on ? (byte)255 : (byte)0;
            }
        }
        return (new(w, h, display), new(w, h, mask));
    }
}
