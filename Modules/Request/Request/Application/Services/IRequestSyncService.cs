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
    /// <param name="forcedSource">When non-null, overrides every inserted/updated row's Source
    /// regardless of what the DTO carries. Pass null to trust the payload per-row.</param>
    Task<IReadOnlyList<RequestTitle>> SyncTitlesAsync(
        Guid requestId,
        List<RequestTitleDto> titles,
        CancellationToken cancellationToken = default,
        string? forcedSource = null);

    /// <summary>
    /// Synchronizes documents for a request. Compares incoming list with existing.
    /// </summary>
    /// <param name="forcedSource">When non-null, overrides every inserted/updated row's Source
    /// regardless of what the DTO carries. Pass null to trust the payload per-row.</param>
    Task SyncDocumentsAsync(
        Domain.Requests.Request request,
        List<RequestDocumentDto> documents,
        CancellationToken cancellationToken = default,
        string? forcedSource = null);
}