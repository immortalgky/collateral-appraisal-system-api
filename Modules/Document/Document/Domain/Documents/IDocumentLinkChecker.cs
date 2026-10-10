namespace Document.Domain.Documents;

public interface IDocumentLinkChecker
{
    /// <summary>
    /// Whether any live link points at the file, read from <c>document.vw_DocumentLinks</c> (every table
    /// holding a DocumentId). Live, unlike <c>Document.ReferenceCount</c>, which is maintained
    /// asynchronously and drifts.
    /// </summary>
    Task<bool> HasLiveLinksAsync(Guid documentId, CancellationToken cancellationToken = default);
}
