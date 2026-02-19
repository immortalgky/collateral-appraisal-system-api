namespace Document.Services;

public interface IImageResizeService
{
    byte[] Resize(string filePath, string size);
    bool IsImage(string mimeType);
    bool IsValidSize(string size);
    string GetResizedMimeType(string originalMimeType);
}
