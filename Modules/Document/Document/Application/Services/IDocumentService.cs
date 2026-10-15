using Document.Domain.Documents.Features.UploadDocument;

namespace Document.Services;

public interface IDocumentService
{
    Task<UploadDocumentResult> UploadAsync(IFormFile file, Guid uploadSessionId, string documentType,
        string documentCategory,
        string? description, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a stream to the staging area and reports what landed there. For an upload being read
    /// off the wire, whose metadata may only arrive after the bytes do.
    /// </summary>
    Task<StagedUpload> StageStreamAsync(
        Stream content,
        string fileName,
        long maxBytes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Registers a file already on the share as a document — the final step of a chunked upload
    /// and of a streamed one, where the bytes are in place and only the row is missing.
    /// </summary>
    /// <param name="knownChecksumBase64">
    /// The SHA-256 of the staged bytes when the caller already has it — a streamed upload hashes
    /// as it writes. Null makes this read the file to compute one.
    /// </param>
    Task<UploadDocumentResult> CreateFromStagedFileAsync(
        Guid documentId,
        string stagedFilePath,
        StagedFileMetadata metadata,
        string? knownChecksumBase64 = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteFileAsync(Guid id, CancellationToken cancellationToken = default);

    Task CopyToAsync(string sourcePath, string destinationPath, bool deleteSource = false,
        CancellationToken cancellationToken = default);
}