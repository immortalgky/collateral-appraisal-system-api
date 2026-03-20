namespace Appraisal.Application.Features.Appraisals.GetAppraisalAppendices;

public class GetAppraisalAppendicesQueryHandler(
    AppraisalDbContext dbContext
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

        // Batch-load gallery photos to resolve DocumentId for each GalleryPhotoId
        var allGalleryPhotoIds = appendices
            .SelectMany(a => a.Documents)
            .Select(d => d.GalleryPhotoId)
            .Distinct()
            .ToList();

        var galleryLookup = await dbContext.AppraisalGallery
            .Where(g => allGalleryPhotoIds.Contains(g.Id))
            .Select(g => new { g.Id, g.DocumentId, g.FileName, g.FilePath, g.FileExtension, g.MimeType, g.FileSizeBytes })
            .ToDictionaryAsync(g => g.Id, cancellationToken);

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
                            var photo = galleryLookup.GetValueOrDefault(d.GalleryPhotoId);
                            return new AppendixDocumentDto(
                                d.Id,
                                d.GalleryPhotoId,
                                photo?.DocumentId ?? Guid.Empty,
                                d.DisplaySequence,
                                photo?.FileName,
                                photo?.FilePath,
                                photo?.FileExtension,
                                photo?.MimeType,
                                photo?.FileSizeBytes
                            );
                        }).ToList()
                );
            }).ToList();

        return new GetAppraisalAppendicesResult(dtos);
    }
}
