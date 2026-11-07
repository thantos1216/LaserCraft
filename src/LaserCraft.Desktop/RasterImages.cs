using Avalonia.Media.Imaging;
using LaserCraft.Domain;
using SkiaSharp;
namespace LaserCraft.Desktop;

public static class RasterImages
{
    public static RasterGrid EngravingGrid(DesignShape shape, CutLayer layer, CancellationToken cancellationToken = default)
    {
        var width = (int)Math.Ceiling(shape.Width / layer.Interval); var height = (int)Math.Ceiling(shape.Height / layer.Interval);
        if (width < 1 || height < 1 || width > 16000 || height > 16000 || (long)width * height > 4_000_000) throw new InvalidDataException("Raster exceeds 16000 samples on one axis or 4 million samples. Increase the image layer line interval.");
        cancellationToken.ThrowIfCancellationRequested();
        using var source = SKBitmap.Decode(Convert.FromBase64String(shape.Image!.DataBase64)) ?? throw new InvalidDataException("Could not decode image.");
        using var resized = source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKFilterMode.Linear)) ?? throw new InvalidDataException("Could not sample image.");
        var pixels = resized.Pixels; var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++) { cancellationToken.ThrowIfCancellationRequested(); for (int x = 0; x < width; x++) { var i = y * width + x; var p = pixels[i]; rgba[i * 4] = p.Red; rgba[i * 4 + 1] = p.Green; rgba[i * 4 + 2] = p.Blue; rgba[i * 4 + 3] = p.Alpha; } }
        return RasterProcessing.ProcessRgba(width, height, rgba, layer.Raster ?? new(), cancellationToken);
    }
    public static Bitmap ProcessedPreview(RasterGrid grid)
    {
        using var bitmap = new SKBitmap(grid.Width, grid.Height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        bitmap.Pixels = grid.Darkness.Select(d => new SKColor((byte)(255 - d), (byte)(255 - d), (byte)(255 - d), 255)).ToArray();
        using var image = SKImage.FromBitmap(bitmap); using var encoded = image.Encode(SKEncodedImageFormat.Png, 100); using var stream = encoded.AsStream(); return grid.Height > grid.Width ? Bitmap.DecodeToHeight(stream, Math.Min(800, grid.Height)) : Bitmap.DecodeToWidth(stream, Math.Min(800, grid.Width));
    }
    public static RasterAsset Import(byte[] bytes)
    {
        if (bytes.Length > 8_000_000) throw new InvalidDataException("Images are limited to 8 MB of original encoded data.");
        using var stream = new SKMemoryStream(bytes); using var codec = SKCodec.Create(stream) ?? throw new InvalidDataException("The file is not a supported raster image.");
        var mime = codec.EncodedFormat switch { SKEncodedImageFormat.Png => "image/png", SKEncodedImageFormat.Jpeg => "image/jpeg", SKEncodedImageFormat.Bmp => "image/bmp", _ => throw new InvalidDataException("Use PNG, JPEG or BMP images in this build.") };
        var asset = new RasterAsset(Guid.NewGuid(), mime, Convert.ToBase64String(bytes), codec.Info.Width, codec.Info.Height); asset.Validate();
        using var decoded = new SKBitmap(codec.Info); if (codec.GetPixels(codec.Info, decoded.GetPixels()) != SKCodecResult.Success) throw new InvalidDataException("Raster image data is incomplete or corrupt.");
        return asset;
    }
    public static void Verify(DesignDocument d)
    {
        foreach (var asset in d.Shapes.Where(s => s.Image is not null).Select(s => s.Image!).DistinctBy(a => a.Id))
        {
            var check = Import(Convert.FromBase64String(asset.DataBase64));
            if (check.PixelWidth != asset.PixelWidth || check.PixelHeight != asset.PixelHeight || check.MimeType != asset.MimeType) throw new InvalidDataException("Embedded image metadata does not match its source data.");
        }
    }
    public static Bitmap Preview(RasterAsset asset)
    {
        using var stream = new MemoryStream(Convert.FromBase64String(asset.DataBase64)); return asset.PixelHeight > asset.PixelWidth ? Bitmap.DecodeToHeight(stream, Math.Min(2048, asset.PixelHeight)) : Bitmap.DecodeToWidth(stream, Math.Min(2048, asset.PixelWidth));
    }
}
