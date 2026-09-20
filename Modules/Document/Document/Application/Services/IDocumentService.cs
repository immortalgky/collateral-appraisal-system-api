using Document.Domain.Documents.Features.UploadDocument;

namespace Document.Services;

public interface IDocumentService
{
    Task<UploadDocumentResult> UploadAsync(IFormFile file, Guid uploadSessionId, string documentType,
        string documentCategory,
        string? description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a file already assembled on the share as a document — the final step of a
    /// chunked upload, where the bytes are in place and only the row is missing.
    /// </summary>
    Task<UploadDocumentResult> CreateFromStagedFileAsync(
        Guid documentId,
        string stagedFilePath,
        ChunkedUploadMeta meta,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(Guid id, CancellationToken cancellationToken = default);

    Task CopyToAsync(string sourcePath, string destinationPath, bool deleteSource = false,
        CancellationToken cancellationToken = default);
}