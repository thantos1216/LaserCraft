using Avalonia.Media.Imaging;
using LaserCraft.Domain;
using SkiaSharp;
namespace LaserCraft.Desktop;

public static class RasterImages
{
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
        using var stream = new MemoryStream(Convert.FromBase64String(asset.DataBase64)); return Bitmap.DecodeToWidth(stream, Math.Min(2048, asset.PixelWidth));
    }
}
