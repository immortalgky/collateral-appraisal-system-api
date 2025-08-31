using Document.Contracts.Documents.Dtos;

namespace Document.Contracts.Documents.Services;

public interface IDocumentValidationService
{
    /// <summary>
    /// Checks if a document with the specified ID exists
    /// </summary>
    /// <param name="documentId">The document ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if document exists, false otherwise</returns>
    Task<bool> DocumentExistsAsync(long documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets document information by ID
    /// </summary>
    /// <param name="documentId">The document ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>DocumentDto if found, null otherwise</returns>
    Task<DocumentDto?> GetDocumentAsync(long documentId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a document can be linked to a request
    /// </summary>
    /// <param name="documentId">The document ID</param>
    /// <param name="relateRequest">The request type trying to link</param>
    /// <param name="relateId">The request ID trying to link</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if linking is allowed, false otherwise</returns>
    Task<bool> CanLinkToRequestAsync(long documentId, string relateRequest, long relateId, CancellationToken cancellationToken = default);
}