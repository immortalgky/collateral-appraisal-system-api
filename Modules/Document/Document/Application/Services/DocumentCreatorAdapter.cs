using Document.Contracts;

namespace Document.Services;

/// <summary>
/// Thin adapter that exposes <see cref="IDocumentCreator"/> (the cross-module port)
/// by delegating to <see cref="DocumentService.UploadFromBytesAsync"/>.
/// Registered in <see cref="DocumentModule"/> as a transient.
/// </summary>
internal sealed class DocumentCreatorAdapter(IDocumentCreatorService documentCreatorService) : IDocumentCreator
{
    public Task<Guid> CreateFromBytesAsync(
        byte[] bytes,
        string fileName,
        string mimeType,
        string documentType,
        string documentCategory,
        string uploadedBy,
        string? uploadedByName,
        CancellationToken ct = default)
        => documentCreatorService.UploadFromBytesAsync(bytes, fileName, mimeType,
            documentType, documentCategory, uploadedBy, uploadedByName, ct);
}
