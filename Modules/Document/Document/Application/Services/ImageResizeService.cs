using Microsoft.Extensions.Options;
using Shared.Configurations;
using SkiaSharp;

namespace Document.Services;

internal class ImageResizeService(IOptions<FileStorageConfiguration> options) : IImageResizeService
{
    private static readonly HashSet<string> ImageMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly Dictionary<string, ImageSizeConfiguration> _sizes = options.Value.ImageVariants.Sizes;

    public bool IsImage(string mimeType) => ImageMimeTypes.Contains(mimeType);

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

        using var inputStream = File.OpenRead(filePath);
        using var original = SKBitmap.Decode(inputStream);

        if (original is null)
            throw new InvalidOperationException("Unable to decode image file.");

        var (targetWidth, targetHeight) = CalculateSize(
            original.Width, original.Height,
            sizeConfig.Width, sizeConfig.Height);

        // Don't upscale — return original bytes if already smaller
        if (targetWidth >= original.Width && targetHeight >= original.Height)
            return File.ReadAllBytes(filePath);

        using var resized = original.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.Medium);
        using var image = SKImage.FromBitmap(resized);

        var format = DetectFormat(filePath);
        var quality = format == SKEncodedImageFormat.Png ? 100 : 85;
        using var data = image.Encode(format, quality);

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
