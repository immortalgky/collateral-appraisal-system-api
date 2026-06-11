namespace Document.Contracts;

/// <summary>
/// Creates a Document from raw bytes and persists it to storage.
/// Consumed by Workflow module to persist server-generated PDFs (e.g. meeting reports)
/// as first-class Document records with a stable <see cref="Guid"/> id.
/// </summary>
public interface IDocumentCreator
{
    /// <summary>
    /// Writes <paramref name="bytes"/> to storage, computes its checksum, creates a Document
    /// aggregate row, and returns the new <see cref="Guid"/> document id.
    /// </summary>
    Task<Guid> CreateFromBytesAsync(
        byte[] bytes,
        string fileName,
        string mimeType,
        string documentType,
        string documentCategory,
        string uploadedBy,
        string? uploadedByName,
        CancellationToken ct = default);
}
