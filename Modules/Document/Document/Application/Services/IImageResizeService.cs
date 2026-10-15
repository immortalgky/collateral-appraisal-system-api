namespace Document.Services;

public interface IImageResizeService
{
    byte[] Resize(string filePath, string size);

    /// <summary>
    /// Whether the image at <paramref name="filePath"/> can be decoded within the configured
    /// memory budget. Reads the file's header only — no pixels are decoded.
    /// </summary>
    /// <param name="refusal">
    /// When the answer is false, a message naming the image's real dimensions, fit to show a user.
    /// </param>
    /// <returns>
    /// True for anything this service cannot read at all (a PDF, a TIFF): such a file is never
    /// decoded by us, so it cannot exhaust memory here, and the extension allow-list owns whether
    /// it belongs in the system.
    /// </returns>
    bool IsWithinDecodeBudget(string filePath, out string refusal);

    bool IsImage(string mimeType);
    bool IsValidSize(string size);
    string GetResizedMimeType(string originalMimeType);
}
