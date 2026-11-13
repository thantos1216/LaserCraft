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
        return RasterProcessing.ProcessRgba(width,height,SampleRgba(shape,width,height,cancellationToken),layer.Raster??new(),cancellationToken);
    }
    public sealed record EditPreview(RasterGrid Original,RasterGrid Adjusted,RasterGrid Laser,int OutputWidth,int OutputHeight)
    {
        public bool OutputSupported => OutputWidth<=16000&&OutputHeight<=16000&&(long)OutputWidth*OutputHeight<=4_000_000;
        public bool Reduced => Original.Width!=OutputWidth||Original.Height!=OutputHeight;
    }
    public static EditPreview PrepareEditPreview(DesignShape shape,ImagePreparationSettings settings,CancellationToken token=default)
    {
        settings.Validate();var outputWidth=(int)Math.Ceiling(shape.Width/settings.Interval);var outputHeight=(int)Math.Ceiling(shape.Height/settings.Interval);
        if(outputWidth<1||outputHeight<1)throw new InvalidDataException("Image needs nonzero dimensions.");
        var scale=Math.Min(1,800d/Math.Max(outputWidth,outputHeight));var width=Math.Max(1,(int)Math.Floor(outputWidth*scale));var height=Math.Max(1,(int)Math.Floor(outputHeight*scale));
        var rgba=SampleRgba(shape,width,height,token);
        var original=RasterProcessing.ProcessRgba(width,height,rgba,new(RasterMode.Grayscale),token);
        var adjusted=RasterProcessing.ProcessRgba(width,height,rgba,settings.Raster with {Mode=RasterMode.Grayscale},token);
        var laser=settings.Raster.Mode==RasterMode.Grayscale?adjusted:RasterProcessing.ProcessRgba(width,height,rgba,settings.Raster,token);
        return new(original,adjusted,laser,outputWidth,outputHeight);
    }
    private static byte[] SampleRgba(DesignShape shape,int width,int height,CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var source = SKBitmap.Decode(Convert.FromBase64String(shape.Image!.DataBase64)) ?? throw new InvalidDataException("Could not decode image.");
        using var resized = source.Resize(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul), new SKSamplingOptions(SKFilterMode.Linear)) ?? throw new InvalidDataException("Could not sample image.");
        var pixels = resized.Pixels; var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++) { cancellationToken.ThrowIfCancellationRequested(); for (int x = 0; x < width; x++) { var i = y * width + x; var p = pixels[i]; rgba[i * 4] = p.Red; rgba[i * 4 + 1] = p.Green; rgba[i * 4 + 2] = p.Blue; rgba[i * 4 + 3] = p.Alpha; } }
        return rgba;
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
