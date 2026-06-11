using Document.Contracts;
using Document.Domain.Documents;
using Microsoft.Extensions.Logging;

namespace Document.Infrastructure;

/// <summary>
/// Loads a document's binary content from disk for use as an email attachment.
/// Reads the file at <c>Document.StoragePath</c> and returns null for deleted / inactive docs.
/// </summary>
internal sealed class DocumentContentProvider(
    IDocumentRepository documentRepository,
    ILogger<DocumentContentProvider> logger) : IDocumentContentProvider
{
    public async Task<DocumentContent?> GetAsync(Guid documentId, CancellationToken ct = default)
    {
        var document = await documentRepository.GetByIdAsync(documentId, ct);

        if (document is null || document.IsDeleted || !document.IsActive)
        {
            logger.LogWarning(
                "Document {DocumentId} not found, deleted, or inactive — skipping as email attachment",
                documentId);
            return null;
        }

        if (!File.Exists(document.StoragePath))
        {
            logger.LogWarning(
                "Document {DocumentId} storage file missing at {Path} — skipping as email attachment",
                documentId, document.StoragePath);
            return null;
        }

        var bytes = await File.ReadAllBytesAsync(document.StoragePath, ct);
        return new DocumentContent(document.FileName, document.MimeType, bytes);
    }
}
