namespace Request.Application.Services;

/// <summary>
/// Service for synchronizing request-related data (titles, documents).
/// </summary>
public class RequestSyncService(
    IRequestTitleRepository titleRepository,
    IDateTimeProvider dateTimeProvider
) : IRequestSyncService
{
    // A file with no upload time (the default date) gets the current app time (ApplicationNow, the clock the
    // audit columns use), as on create - never 0001-01-01.
    // An empty placeholder (no file) has no upload, so it keeps what the caller sent.
    private DateTime? UploadedAtFor(Guid? documentId, DateTime? uploadedAt) =>
        documentId.HasValue && uploadedAt == default(DateTime) ? dateTimeProvider.ApplicationNow : uploadedAt;

    // The label of a row being ADDED. A forced label (the Followup resubmit stamps FOLLOWUP) applies only to a row
    // that ends up with a file: an empty placeholder is not an answer to anything, and a FOLLOWUP label on it
    // would stick to whatever staff upload into it later.
    private static string NewRowSource(string? forcedSource, string? payloadSource, Guid? documentId) =>
        forcedSource is not null && documentId.HasValue ? forcedSource : ClientDocumentSource.Normalize(payloadSource);

    // The label of an existing row after a sync. A server-stamped FOLLOWUP row never changes; a row whose file
    // changed takes the forced label only if it still has a file, else the payload's (whitelisted) one.
    private static string? UpdatedSource(
        string? storedSource, Guid? storedDocumentId, Guid? newDocumentId, string? forcedSource, string? payloadSource) =>
        ReStamped(storedSource, storedDocumentId, newDocumentId)
            ? NewRowSource(forcedSource, payloadSource, newDocumentId)
            : storedSource;

    // A row that receives a different file (a carried PREV file replaced, or an empty saved slot filled)
    // takes the payload's Source; a server-stamped FOLLOWUP row never changes label. Every Source taken from
    // a payload goes through ClientDocumentSource, whoever the caller is.
    private static bool ReStamped(string? storedSource, Guid? storedDocumentId, Guid? newDocumentId) =>
        !ClientDocumentSource.IsFollowUp(storedSource) && storedDocumentId != newDocumentId;

    public async Task<IReadOnlyList<RequestTitle>> SyncTitlesAsync(
        Guid requestId,
        List<RequestTitleDto> titles,
        CancellationToken cancellationToken = default,
        string? forcedSource = null)
    {
        var incomingTitles = titles;
        var existingTitles = (await titleRepository.GetByRequestIdWithDocumentsAsync(requestId, cancellationToken))
            .ToList();

        var existingById = existingTitles
            .Where(t => t.Id != Guid.Empty)
            .ToDictionary(t => t.Id);
        var existingIds = existingById.Keys.ToHashSet();

        var incomingIds = incomingTitles
            .Where(t => t.Id.HasValue && t.Id.Value != Guid.Empty)
            .Select(t => t.Id!.Value)
            .ToHashSet();

        // DELETE: Existing titles not in an incoming list
        var toDeleteIds = existingIds.Except(incomingIds);
        foreach (var id in toDeleteIds) await titleRepository.DeleteAsync(existingById[id], cancellationToken);

        var resultTitles = new List<RequestTitle>();

        // The incoming list order wins: each title is stamped with its 1-based position, new or updated.
        var numbered = incomingTitles.Select((dto, i) => (dto, sequence: i + 1)).ToList();

        // CREATE: Titles without ID
        var toCreate = numbered.Where(t => !t.dto.Id.HasValue || t.dto.Id.Value == Guid.Empty);
        foreach (var (dto, sequence) in toCreate)
        {
            var title = TitleFactory.Create(
                dto.CollateralType,
                dto.ToRequestTitleData() with { RequestId = requestId });
            title.SetSequenceNumber(sequence);

            SyncTitleDocuments(title, dto.Documents, forcedSource);
            await titleRepository.AddAsync(title, cancellationToken);
            resultTitles.Add(title);
        }

        // UPDATE: Titles with matching ID
        var toUpdate = numbered.Where(t => t.dto.Id.HasValue && existingIds.Contains(t.dto.Id.Value));
        foreach (var (dto, sequence) in toUpdate)
        {
            var existing = existingById[dto.Id!.Value];

            if (dto.CollateralType != existing.CollateralType)
            {
                // CollateralType changed - must delete and recreate (EF Core TPH limitation)
                await titleRepository.DeleteAsync(existing, cancellationToken);

                var newTitle = TitleFactory.Create(
                    dto.CollateralType,
                    dto.ToRequestTitleData() with { RequestId = requestId });
                newTitle.SetSequenceNumber(sequence);

                // Reset document IDs for a new title. The rows are re-created, so a stored FOLLOWUP file the
                // client echoes back keeps its label (and only that: a client cannot make a FOLLOWUP row).
                var storedLabels = existing.Documents
                    .Where(d => d.DocumentId.HasValue)
                    .GroupBy(d => d.DocumentId!.Value)
                    .ToDictionary(g => g.Key, g => g.First().Source);
                var docsWithoutIds = dto.Documents
                    .Select(d => d with { Id = null })
                    .ToList();
                SyncTitleDocuments(newTitle, docsWithoutIds, forcedSource, storedLabels);

                await titleRepository.AddAsync(newTitle, cancellationToken);
                resultTitles.Add(newTitle);
            }
            else
            {
                existing.Update(dto.ToRequestTitleData());
                existing.SetSequenceNumber(sequence);
                SyncTitleDocuments(existing, dto.Documents, forcedSource);
                resultTitles.Add(existing);
            }
        }

        // In the order just stamped, not created-then-updated, so a caller taking [0] gets the requester's first title.
        return resultTitles.OrderBy(t => t.SequenceNumber).ToList();
    }

    public Task SyncDocumentsAsync(
        Domain.Requests.Request request,
        List<RequestDocumentDto> documents,
        CancellationToken cancellationToken = default,
        string? forcedSource = null)
    {
        var incomingDocs = documents;
        var existingDocs = request.Documents.ToList();

        var existingIds = existingDocs
            .Where(d => d.Id != Guid.Empty)
            .Select(d => d.Id)
            .ToHashSet();

        var incomingIds = incomingDocs
            .Where(d => d.Id.HasValue && d.Id.Value != Guid.Empty)
            .Select(d => d.Id!.Value)
            .ToHashSet();

        // DELETE: Existing docs not in the incoming list
        var toDeleteIds = existingIds.Except(incomingIds);
        foreach (var id in toDeleteIds) request.RemoveDocument(id);

        // CREATE: Docs without ID — forcedSource overrides payload Source on new rows
        foreach (var dto in incomingDocs.Where(d => !d.Id.HasValue || d.Id.Value == Guid.Empty))
            request.AddDocument(new RequestDocumentData(
                dto.DocumentId,
                dto.DocumentType,
                dto.FileName,
                dto.Prefix,
                dto.Set,
                dto.Notes,
                dto.FilePath,
                NewRowSource(forcedSource, dto.Source, dto.DocumentId),
                dto.IsRequired,
                dto.UploadedBy,
                dto.UploadedByName,
                UploadedAtFor(dto.DocumentId, dto.UploadedAt)
            ));

        // UPDATE: Docs with matching ID — only when something actually changed.
        // Source is intentionally PRESERVED on update (audit-trail data — a data-fix resubmit
        // must not silently relabel a previously FOLLOWUP-sourced row back to REQUEST). The exception: a
        // non-FOLLOWUP row that receives a different file (see ReStamped) takes the payload's.
        var existingById = existingDocs.Where(d => d.Id != Guid.Empty).ToDictionary(d => d.Id);
        foreach (var dto in incomingDocs.Where(d => d.Id.HasValue && existingIds.Contains(d.Id.Value)))
        {
            var existing = existingById[dto.Id!.Value];
            if (!HasRequestDocumentChanges(dto, existing))
                continue;

            request.UpdateDocument(dto.Id!.Value, new RequestDocumentData(
                dto.DocumentId,
                dto.DocumentType,
                dto.FileName,
                dto.Prefix,
                dto.Set,
                dto.Notes,
                dto.FilePath,
                UpdatedSource(existing.Source, existing.DocumentId, dto.DocumentId, forcedSource, dto.Source),
                dto.IsRequired,
                dto.UploadedBy,
                dto.UploadedByName,
                UploadedAtFor(dto.DocumentId, dto.UploadedAt)
            ));
        }

        return Task.CompletedTask;
    }

    private static bool HasRequestDocumentChanges(RequestDocumentDto dto, RequestDocument existing)
    {
        return dto.DocumentId != existing.DocumentId ||
               dto.DocumentType != existing.DocumentType ||
               dto.FileName != existing.FileName ||
               dto.Prefix != existing.Prefix ||
               dto.Set != existing.Set ||
               dto.Notes != existing.Notes ||
               dto.FilePath != existing.FilePath ||
               dto.IsRequired != existing.IsRequired ||
               dto.UploadedBy != existing.UploadedBy ||
               dto.UploadedByName != existing.UploadedByName;
    }

    private void SyncTitleDocuments(RequestTitle title, List<RequestTitleDocumentDto> documents,
        string? forcedSource = null, IReadOnlyDictionary<Guid, string?>? storedLabels = null)
    {
        var existingDocs = title.Documents.ToList();
        var existingIds = existingDocs
            .Where(d => d.Id != Guid.Empty)
            .Select(d => d.Id)
            .ToHashSet();

        var incomingIds = documents
            .Where(d => d.Id.HasValue && d.Id.Value != Guid.Empty)
            .Select(d => d.Id!.Value)
            .ToHashSet();

        // DELETE: Existing docs not in incoming list
        var toDeleteIds = existingIds.Except(incomingIds);
        foreach (var id in toDeleteIds) title.RemoveDocument(id);

        // CREATE: Docs without ID
        foreach (var dto in documents.Where(d => !d.Id.HasValue || d.Id.Value == Guid.Empty))
        {
            // A title re-created for a new collateral type: a file it already held keeps its stored label, so only
            // a genuinely new file takes the forced one.
            var source = dto.DocumentId is { } fileId && storedLabels is not null && storedLabels.TryGetValue(fileId, out var stored)
                ? ClientDocumentSource.Normalize(stored, echoesStoredFollowUp: true)
                : NewRowSource(forcedSource, dto.Source, dto.DocumentId);
            var data = dto.ToTitleDocumentData() with
            {
                Source = source,
                UploadedAt = UploadedAtFor(dto.DocumentId, dto.UploadedAt)!.Value
            };
            title.AddDocument(data);
        }

        // UPDATE: Docs with matching ID (only if changed).
        // Source is intentionally PRESERVED on update — audit-trail data must not flip on a
        // data-fix resubmit (forcedSource only takes effect on CREATE).
        foreach (var dto in documents.Where(d => d.Id.HasValue && existingIds.Contains(d.Id.Value)))
        {
            var existing = existingDocs.FirstOrDefault(e => e.Id == dto.Id!.Value);
            if (existing is not null && HasDocumentChanges(dto, existing))
            {
                var data = dto.ToTitleDocumentData() with
                {
                    Source = UpdatedSource(existing.Source, existing.DocumentId, dto.DocumentId, forcedSource, dto.Source),
                    // The same file keeps its stored upload time when the payload leaves it out (a notes-only edit).
                    UploadedAt = dto.UploadedAt == default && dto.DocumentId == existing.DocumentId
                        ? existing.UploadedAt
                        : UploadedAtFor(dto.DocumentId, dto.UploadedAt)!.Value
                };
                title.UpdateDocument(dto.Id!.Value, data);
            }
        }
    }

    private static bool HasDocumentChanges(RequestTitleDocumentDto dto, TitleDocument existing)
    {
        // Source intentionally excluded — it's preserved across updates (re-stamped only when the
        // DocumentId changes, which is itself a change), so a payload Source difference alone
        // should not force an Update call.
        return dto.DocumentId != existing.DocumentId ||
               dto.DocumentType != existing.DocumentType ||
               dto.FileName != existing.FileName ||
               dto.Prefix != existing.Prefix ||
               dto.Set != existing.Set ||
               dto.Notes != existing.Notes ||
               dto.FilePath != existing.FilePath ||
               dto.IsRequired != existing.IsRequired ||
               dto.UploadedBy != existing.UploadedBy ||
               dto.UploadedByName != existing.UploadedByName;
    }
}