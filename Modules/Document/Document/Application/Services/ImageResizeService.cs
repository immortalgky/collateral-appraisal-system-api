using Microsoft.Extensions.Options;
using Shared.Configurations;
using SkiaSharp;

namespace Document.Services;

internal class ImageResizeService(
    IOptions<FileStorageConfiguration> options,
    ILogger<ImageResizeService> logger) : IImageResizeService
{
    private static readonly HashSet<string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly Dictionary<string, ImageSizeConfiguration> _sizes = options.Value.ImageVariants.Sizes;
    private readonly long _maxDecodeBytes = options.Value.MaxImageDecodeBytes;

    /// <summary>Bytes one pixel occupies once decoded — 8-bit RGBA.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>
    /// The deepest scale any codec here offers while decoding: JPEG can go to an eighth, which is
    /// what makes a 200 megapixel photo affordable. Asking for a smaller scale than a codec
    /// supports is not an error — it answers with the nearest size it can actually produce, which
    /// for PNG is the full image.
    /// </summary>
    private const float SmallestSupportedScale = 0.125f;

    /// <summary>
    /// The largest original handed back untouched when the requested thumbnail is bigger than the
    /// image itself. Past this the response buffer is the problem rather than the decoder: a
    /// 300 MB file whose pixel dimensions happen to be small is still 300 MB held in memory.
    /// </summary>
    private const long MaxPassThroughBytes = 10L * 1024 * 1024;

    public bool IsImage(string mimeType) => ImageMimeTypes.Contains(mimeType);

    public bool IsWithinDecodeBudget(string filePath, out string refusal)
    {
        refusal = string.Empty;

        using var stream = File.OpenRead(filePath);
        using var codec = SKCodec.Create(stream);

        // Not an image this service can read (a PDF, a TIFF). It is never decoded here, so it
        // cannot exhaust memory here either; whether it belongs in the system at all is the
        // extension allow-list's question, not this one's.
        if (codec is null) return true;

        var info = codec.Info;
        var smallest = codec.GetScaledDimensions(SmallestSupportedScale);
        var decodeBytes = (long)smallest.Width * smallest.Height * BytesPerPixel;

        if (decodeBytes <= _maxDecodeBytes) return true;

        refusal =
            $"This image is too large for the system to process ({info.Width}×{info.Height} pixels). " +
            "Save it at a lower resolution, or as a JPEG, and upload it again.";
        return false;
    }

    public bool IsValidSize(string size) =>
        _sizes.ContainsKey(size, StringComparer.OrdinalIgnoreCase);

    public string GetResizedMimeType(string originalMimeType) =>
        originalMimeType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
            ? "image/png"
            : "image/jpeg";

    public byte[] Resize(string filePath, string size)
    {
        var sizeConfig = _sizes.First(kvp =>
            kvp.Key.Equals(size, StringComparison.OrdinalIgnoreCase)).Value;

        var format = DetectFormat(filePath);

        using var inputStream = File.OpenRead(filePath);
        using var codec = SKCodec.Create(inputStream);

        if (codec is null)
            throw new InvalidOperationException("Unable to decode image file.");

        // The header alone gives the dimensions. Nothing is decoded until we know what decoding
        // would cost — the previous version read every pixel into memory first and asked
        // afterwards, which is how one gallery of scanned deeds could take the node down.
        var info = codec.Info;
        var (targetWidth, targetHeight) = CalculateSize(
            info.Width, info.Height,
            sizeConfig.Width, sizeConfig.Height);

        // Don't upscale — return the original, while the original is small enough to hold.
        if (targetWidth >= info.Width && targetHeight >= info.Height)
            return new FileInfo(filePath).Length <= MaxPassThroughBytes
                ? File.ReadAllBytes(filePath)
                : Placeholder(sizeConfig, format);

        // Decode straight to the smallest size the codec offers that still covers the thumbnail,
        // instead of decoding the whole image and shrinking it afterwards.
        var scale = Math.Max(
            (float)targetWidth / info.Width,
            (float)targetHeight / info.Height);
        var scaled = codec.GetScaledDimensions(scale);

        var decodeBytes = (long)scaled.Width * scaled.Height * BytesPerPixel;
        if (decodeBytes > _maxDecodeBytes)
        {
            // Serving a grey tile keeps one oversized image from becoming an outage, and keeps
            // every caller — img tags in a dozen galleries — working without a special case.
            logger.LogWarning(
                "Thumbnail skipped for {FilePath}: decoding it needs {DecodeBytes} bytes ({Width}x{Height}), over the {Budget} budget",
                filePath, decodeBytes, info.Width, info.Height, _maxDecodeBytes);
            return Placeholder(sizeConfig, format);
        }

        using var decoded = SKBitmap.Decode(
            codec,
            new SKImageInfo(scaled.Width, scaled.Height, info.ColorType, info.AlphaType));
        if (decoded is null)
            return FallBack(filePath, sizeConfig, format, "the codec refused to decode it");

        using var resized = decoded.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);
        if (resized is null)
            return FallBack(filePath, sizeConfig, format, "resampling failed");

        using var image = SKImage.FromBitmap(resized);

        var quality = format == SKEncodedImageFormat.Png ? 100 : 85;
        using var data = image.Encode(format, quality);

        return data.ToArray();
    }

    /// <summary>
    /// What to serve when the thumbnail could not be made for a reason other than its size: the
    /// original, which is what this service always used to return, unless the original is itself
    /// too large to hold in a response — and a warning either way, because a gallery quietly
    /// turning grey after a deploy with nothing in the log is not a failure anyone can chase.
    /// </summary>
    private byte[] FallBack(
        string filePath,
        ImageSizeConfiguration sizeConfig,
        SKEncodedImageFormat format,
        string reason)
    {
        var length = new FileInfo(filePath).Length;

        logger.LogWarning(
            "Thumbnail could not be produced for {FilePath} ({Reason}); serving {Served}",
            filePath, reason, length <= MaxPassThroughBytes ? "the original" : "a placeholder");

        return length <= MaxPassThroughBytes
            ? File.ReadAllBytes(filePath)
            : Placeholder(sizeConfig, format);
    }

    /// <summary>
    /// A plain grey tile standing in for a thumbnail that cannot be produced. Encoded in the
    /// original's format so it still matches the content type the endpoint advertises.
    /// </summary>
    private static byte[] Placeholder(ImageSizeConfiguration sizeConfig, SKEncodedImageFormat format)
    {
        var width = Math.Max(1, sizeConfig.Width);
        var height = Math.Max(1, sizeConfig.Height);

        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0xE5, 0xE7, 0xEB));

        using var stroke = new SKPaint
        {
            Color = new SKColor(0xB0, 0xB6, 0xBE),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2,
            IsAntialias = true
        };
        canvas.DrawRect(1, 1, width - 2, height - 2, stroke);
        canvas.DrawLine(width * 0.3f, height * 0.5f, width * 0.7f, height * 0.5f, stroke);

        using var image = surface.Snapshot();
        using var data = image.Encode(format, 90);

        return data.ToArray();
    }

    private static (int width, int height) CalculateSize(
        int originalWidth, int originalHeight,
        int maxWidth, int maxHeight)
    {
        var ratioX = (double)maxWidth / originalWidth;
        var ratioY = (double)maxHeight / originalHeight;
        var ratio = Math.Min(ratioX, ratioY);

        return (
            (int)Math.Round(originalWidth * ratio),
            (int)Math.Round(originalHeight * ratio)
        );
    }

    private static SKEncodedImageFormat DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath);
        return ext.ToLowerInvariant() switch
        {
            ".png" => SKEncodedImageFormat.Png,
            ".webp" => SKEncodedImageFormat.Webp,
            _ => SKEncodedImageFormat.Jpeg
        };
    }
}

internal static class DictionaryExtensions
{
    public static bool ContainsKey(this Dictionary<string, ImageSizeConfiguration> dict,
        string key, StringComparer comparer)
    {
        return dict.Keys.Any(k => comparer.Compare(k, key) == 0);
    }
}
