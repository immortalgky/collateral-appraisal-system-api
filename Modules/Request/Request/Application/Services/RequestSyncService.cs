namespace Request.Application.Services;

/// <summary>
/// Service for synchronizing request-related data (titles, documents).
/// </summary>
public class RequestSyncService(
    IRequestTitleRepository titleRepository
) : IRequestSyncService
{
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

        // CREATE: Titles without ID
        var toCreate = incomingTitles.Where(t => !t.Id.HasValue || t.Id.Value == Guid.Empty);
        foreach (var dto in toCreate)
        {
            var title = TitleFactory.Create(
                dto.CollateralType,
                dto.ToRequestTitleData() with { RequestId = requestId });

            SyncTitleDocuments(title, dto.Documents, forcedSource);
            await titleRepository.AddAsync(title, cancellationToken);
            resultTitles.Add(title);
        }

        // UPDATE: Titles with matching ID
        var toUpdate = incomingTitles.Where(t => t.Id.HasValue && existingIds.Contains(t.Id.Value));
        foreach (var dto in toUpdate)
        {
            var existing = existingById[dto.Id!.Value];

            if (dto.CollateralType != existing.CollateralType)
            {
                // CollateralType changed - must delete and recreate (EF Core TPH limitation)
                await titleRepository.DeleteAsync(existing, cancellationToken);

                var newTitle = TitleFactory.Create(
                    dto.CollateralType,
                    dto.ToRequestTitleData() with { RequestId = requestId });

                // Reset document IDs for a new title
                var docsWithoutIds = dto.Documents
                    .Select(d => d with { Id = null })
                    .ToList();
                SyncTitleDocuments(newTitle, docsWithoutIds, forcedSource);

                await titleRepository.AddAsync(newTitle, cancellationToken);
                resultTitles.Add(newTitle);
            }
            else
            {
                existing.Update(dto.ToRequestTitleData());
                SyncTitleDocuments(existing, dto.Documents, forcedSource);
                resultTitles.Add(existing);
            }
        }

        return resultTitles;
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
                forcedSource ?? dto.Source,
                dto.IsRequired,
                dto.UploadedBy,
                dto.UploadedByName,
                dto.UploadedAt
            ));

        // UPDATE: Docs with matching ID — only when something actually changed.
        // Source is intentionally PRESERVED on update (audit-trail data — a data-fix resubmit
        // must not silently relabel a previously FOLLOWUP-sourced row back to REQUEST).
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
                existing.Source,
                dto.IsRequired,
                dto.UploadedBy,
                dto.UploadedByName,
                dto.UploadedAt
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

    private static void SyncTitleDocuments(RequestTitle title, List<RequestTitleDocumentDto> documents,
        string? forcedSource = null)
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
            var data = dto.ToTitleDocumentData();
            title.AddDocument(forcedSource is null ? data : data with { Source = forcedSource });
        }

        // UPDATE: Docs with matching ID (only if changed).
        // Source is intentionally PRESERVED on update — audit-trail data must not flip on a
        // data-fix resubmit (forcedSource only takes effect on CREATE).
        foreach (var dto in documents.Where(d => d.Id.HasValue && existingIds.Contains(d.Id.Value)))
        {
            var existing = existingDocs.FirstOrDefault(e => e.Id == dto.Id!.Value);
            if (existing is not null && HasDocumentChanges(dto, existing))
            {
                var data = dto.ToTitleDocumentData() with { Source = existing.Source };
                title.UpdateDocument(dto.Id!.Value, data);
            }
        }
    }

    private static bool HasDocumentChanges(RequestTitleDocumentDto dto, TitleDocument existing)
    {
        // Source intentionally excluded — it's preserved across updates, so a payload Source
        // difference should not force an Update call.
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