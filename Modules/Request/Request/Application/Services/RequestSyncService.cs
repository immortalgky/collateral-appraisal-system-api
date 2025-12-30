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
        CancellationToken cancellationToken = default)
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

            SyncTitleDocuments(title, dto.Documents);
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
                SyncTitleDocuments(newTitle, docsWithoutIds);

                await titleRepository.AddAsync(newTitle, cancellationToken);
                resultTitles.Add(newTitle);
            }
            else
            {
                existing.Update(dto.ToRequestTitleData());
                SyncTitleDocuments(existing, dto.Documents);
                resultTitles.Add(existing);
            }
        }

        return resultTitles;
    }

    public Task SyncDocumentsAsync(
        Domain.Requests.Request request,
        List<RequestDocumentDto> documents,
        CancellationToken cancellationToken = default)
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

        // CREATE: Docs without ID
        foreach (var dto in incomingDocs.Where(d => !d.Id.HasValue || d.Id.Value == Guid.Empty))
            request.AddDocument(new RequestDocumentData(
                dto.DocumentId,
                dto.DocumentType,
                dto.FileName,
                dto.Prefix,
                dto.Set,
                dto.Notes,
                dto.FilePath,
                dto.Source,
                dto.IsRequired,
                dto.UploadedBy,
                dto.UploadedByName,
                dto.UploadedAt
            ));

        // UPDATE: Docs with matching ID
        foreach (var dto in incomingDocs.Where(d => d.Id.HasValue && existingIds.Contains(d.Id.Value)))
            request.UpdateDocument(dto.Id!.Value, new RequestDocumentData(
                dto.DocumentId,
                dto.DocumentType,
                dto.FileName,
                dto.Prefix,
                dto.Set,
                dto.Notes,
                dto.FilePath,
                dto.Source,
                dto.IsRequired,
                dto.UploadedBy,
                dto.UploadedByName,
                dto.UploadedAt
            ));

        return Task.CompletedTask;
    }

    private static void SyncTitleDocuments(RequestTitle title, List<RequestTitleDocumentDto> documents)
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
            title.AddDocument(dto.ToTitleDocumentData());

        // UPDATE: Docs with matching ID (only if changed)
        foreach (var dto in documents.Where(d => d.Id.HasValue && existingIds.Contains(d.Id.Value)))
        {
            var existing = existingDocs.FirstOrDefault(e => e.Id == dto.Id!.Value);
            if (existing is not null && HasDocumentChanges(dto, existing))
                title.UpdateDocument(dto.Id!.Value, dto.ToTitleDocumentData());
        }
    }

    private static bool HasDocumentChanges(RequestTitleDocumentDto dto, TitleDocument existing)
    {
        return dto.DocumentId != existing.DocumentId ||
               dto.DocumentType != existing.DocumentType ||
               dto.Filename != existing.Filename ||
               dto.Prefix != existing.Prefix ||
               dto.Set != existing.Set ||
               dto.DocumentDescription != existing.DocumentDescription ||
               dto.FilePath != existing.FilePath ||
               dto.CreatedWorkstation != existing.CreatedWorkstation ||
               dto.IsRequired != existing.IsRequired ||
               dto.UploadedBy != existing.UploadedBy ||
               dto.UploadedByName != existing.UploadedByName;
    }
}