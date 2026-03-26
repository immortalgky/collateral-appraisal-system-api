using Document.Domain.Documents.Features.UploadDocument;

namespace Document.Services;

public interface IDocumentService
{
    Task<UploadDocumentResult> UploadAsync(IFormFile file, Guid uploadSessionId, string documentType,
        string documentCategory,
        string? description, CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(Guid id, CancellationToken cancellationToken = default);

    Task<string> CalculateChecksumAsync(Stream stream, CancellationToken cancellationToken = default);

    Task CopyToAsync(string sourcePath, string destinationPath, bool deleteSource = false,
        CancellationToken cancellationToken = default);
}