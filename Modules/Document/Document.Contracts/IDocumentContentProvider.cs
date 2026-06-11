namespace Document.Contracts;

/// <summary>
/// Retrieves the binary content of a stored document by its ID.
/// Implemented in the Document module; consumed by the Notification module's
/// <c>DocumentAttachmentResolver</c> to load manual email attachments.
/// </summary>
public interface IDocumentContentProvider
{
    /// <summary>
    /// Returns the file content for the given <paramref name="documentId"/>,
    /// or <c>null</c> if the document does not exist, is deleted, or is inactive.
    /// </summary>
    Task<DocumentContent?> GetAsync(Guid documentId, CancellationToken ct = default);
}

/// <summary>The in-memory representation of a document file ready to attach to an email.</summary>
public sealed record DocumentContent(string FileName, string MimeType, byte[] Bytes);
