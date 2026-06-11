namespace Document.Services;

/// <summary>
/// Internal service contract extended by <see cref="DocumentService"/> to expose
/// the bytes-based upload path. The public-facing port is <see cref="Document.Contracts.IDocumentCreator"/>.
/// </summary>
internal interface IDocumentCreatorService
{
    Task<Guid> UploadFromBytesAsync(
        byte[] bytes,
        string fileName,
        string mimeType,
        string documentType,
        string documentCategory,
        string uploadedBy,
        string? uploadedByName,
        CancellationToken cancellationToken = default);
}
