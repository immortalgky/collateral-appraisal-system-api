namespace Request.Application.Services;

/// <summary>
/// Service for synchronizing request-related data (titles, documents).
/// Handles add/update/delete based on an incoming list compared to existing data.
/// Does NOT perform validation - handlers call Validate() after sync if needed.
/// </summary>
public interface IRequestSyncService
{
    /// <summary>
    /// Synchronizes titles for a request. Compares an incoming list with existing:
    /// - Items with ID that exists = Update
    /// - Items without ID (or empty GUID) = Create
    /// - Existing items not in an incoming list = Delete
    /// - If CollateralType changes = Delete old + Create new (EF Core TPH limitation)
    /// </summary>
    Task<IReadOnlyList<RequestTitle>> SyncTitlesAsync(
        Guid requestId,
        List<RequestTitleDto> titles,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Synchronizes documents for a request. Compares incoming list with existing.
    /// </summary>
    Task SyncDocumentsAsync(
        Domain.Requests.Request request,
        List<RequestDocumentDto> documents,
        CancellationToken cancellationToken = default);
}