using Dapper;

namespace Appraisal.Application.Features.Appraisals.GetAppraisalAppendices;

public class GetAppraisalAppendicesQueryHandler(
    AppraisalDbContext dbContext,
    ISqlConnectionFactory connectionFactory
) : IQueryHandler<GetAppraisalAppendicesQuery, GetAppraisalAppendicesResult>
{
    public async Task<GetAppraisalAppendicesResult> Handle(
        GetAppraisalAppendicesQuery query,
        CancellationToken cancellationToken)
    {
        // Load appendices with documents (Include only works without Join)
        var appendices = await dbContext.AppraisalAppendices
            .Include(a => a.Documents)
            .Where(a => a.AppraisalId == query.AppraisalId)
            .OrderBy(a => a.SortOrder)
            .ToListAsync(cancellationToken);

        // Load appendix types as a lookup dictionary
        var typeIds = appendices.Select(a => a.AppendixTypeId).Distinct().ToList();
        var typesById = await dbContext.AppendixTypes
            .Where(t => typeIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, cancellationToken);

        var allDocuments = appendices.SelectMany(a => a.Documents).ToList();

        // Batch-load gallery photos (image path) to resolve each one's underlying DocumentId
        var galleryPhotoIds = allDocuments
            .Where(d => d.GalleryPhotoId.HasValue)
            .Select(d => d.GalleryPhotoId!.Value)
            .Distinct()
            .ToList();

        var galleryLookup = await dbContext.AppraisalGallery
            .Where(g => galleryPhotoIds.Contains(g.Id))
            .Select(g => new
            {
                g.Id, g.DocumentId, g.FileName, g.FilePath, g.FileExtension, g.MimeType, g.FileSizeBytes,
                g.UploadedAt, g.UploadedBy, g.UploadedByName
            })
            .ToDictionaryAsync(g => g.Id, cancellationToken);

        // document.Documents is the authoritative source for MimeType/FileName/FilePath/FileSizeBytes —
        // the denormalized AppraisalGallery columns are client-supplied on upload and frequently NULL.
        // Coalesce onto it for both link paths: gallery photos (via their DocumentId) and the
        // PDF path's direct DocumentId (which has no gallery row at all).
        var documentIds = galleryLookup.Values.Select(g => g.DocumentId)
            .Concat(allDocuments.Where(d => d.DocumentId.HasValue).Select(d => d.DocumentId!.Value))
            .Distinct()
            .ToList();

        var connection = connectionFactory.GetOpenConnection();
        var documentRows = documentIds.Count == 0
            ? []
            : (await connection.QueryAsync<DocumentFileRow>(
                """
                SELECT [Id], [FileName], [StoragePath] AS FilePath, [FileExtension], [MimeType],
                       [FileSizeBytes], [UploadedAt], [UploadedBy], [UploadedByName]
                FROM [document].[Documents]
                WHERE [Id] IN @DocumentIds
                  AND [IsActive] = 1
                  AND [IsDeleted] = 0
                """,
                new { DocumentIds = documentIds })).ToList();
        var documentLookup = documentRows.ToDictionary(d => d.Id);

        var dtos = appendices
            .Where(a => typesById.ContainsKey(a.AppendixTypeId))
            .Select(a =>
            {
                var type = typesById[a.AppendixTypeId];
                return new AppraisalAppendixDto(
                    a.Id,
                    a.AppendixTypeId,
                    type.Code,
                    type.Name,
                    a.SortOrder,
                    a.LayoutColumns,
                    a.Documents
                        .OrderBy(d => d.DisplaySequence)
                        .Select(d =>
                        {
                            if (d.GalleryPhotoId is { } galleryPhotoId)
                            {
                                var photo = galleryLookup.GetValueOrDefault(galleryPhotoId);
                                var file = photo is not null
                                    ? documentLookup.GetValueOrDefault(photo.DocumentId)
                                    : null;

                                return new AppendixDocumentDto(
                                    d.Id,
                                    galleryPhotoId,
                                    photo?.DocumentId ?? Guid.Empty,
                                    d.DisplaySequence,
                                    file?.FileName ?? photo?.FileName,
                                    file?.FilePath ?? photo?.FilePath,
                                    file?.FileExtension ?? photo?.FileExtension,
                                    file?.MimeType ?? photo?.MimeType,
                                    file?.FileSizeBytes ?? photo?.FileSizeBytes,
                                    photo?.UploadedAt,
                                    photo?.UploadedBy,
                                    photo?.UploadedByName
                                );
                            }

                            // PDF path: no gallery row exists, resolve straight from document.Documents.
                            var documentId = d.DocumentId!.Value;
                            var doc = documentLookup.GetValueOrDefault(documentId);

                            return new AppendixDocumentDto(
                                d.Id,
                                Guid.Empty,
                                documentId,
                                d.DisplaySequence,
                                doc?.FileName,
                                doc?.FilePath,
                                doc?.FileExtension,
                                doc?.MimeType,
                                doc?.FileSizeBytes,
                                doc?.UploadedAt,
                                doc?.UploadedBy,
                                doc?.UploadedByName
                            );
                        })
                        .ToList()
                );
            })
            .ToList();

        return new GetAppraisalAppendicesResult(dtos);
    }

    private sealed class DocumentFileRow
    {
        public Guid Id { get; init; }
        public string? FileName { get; init; }
        public string? FilePath { get; init; }
        public string? FileExtension { get; init; }
        public string? MimeType { get; init; }
        public long? FileSizeBytes { get; init; }
        public DateTime? UploadedAt { get; init; }
        public string? UploadedBy { get; init; }
        public string? UploadedByName { get; init; }
    }
}
