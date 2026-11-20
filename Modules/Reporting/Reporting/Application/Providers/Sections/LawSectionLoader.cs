using System.Data;
using Reporting.Application.Models.Sections;

namespace Reporting.Application.Providers.Sections;

/// <summary>
/// Loads the appraiser's Law &amp; Regulation records (book section 4) in ONE round trip:
/// appraisal.LawAndRegulations, appraisal.LawAndRegulationImages -> AppraisalGallery -> document.Documents
/// (file path / mime resolved exactly like AppendixSectionLoader), and the TH labels of parameter group
/// LawRegulationHeader. Order matches GetLawAndRegulationsQueryHandler: HeaderCode (ordinal), CreatedAt, Id.
/// </summary>
internal static class LawSectionLoader
{
    public static async Task<IReadOnlyList<LawRecord>> LoadAsync(
        IDbConnection connection, Guid appraisalId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, HeaderCode, Remark, CreatedAt
            FROM appraisal.LawAndRegulations
            WHERE AppraisalId = @AppraisalId;

            SELECT i.LawAndRegulationId, i.DisplaySequence, i.Title, i.Description,
                   COALESCE(d.StoragePath, g.FilePath) AS FilePath,
                   COALESCE(d.MimeType, g.MimeType)    AS MimeType
            FROM appraisal.LawAndRegulationImages i
            JOIN appraisal.LawAndRegulations r        ON r.Id = i.LawAndRegulationId
            JOIN appraisal.AppraisalGallery g         ON g.Id = i.GalleryPhotoId
            LEFT JOIN document.Documents d            ON d.Id = g.DocumentId
            WHERE r.AppraisalId = @AppraisalId
              -- d is the unfiltered join: g.FilePath/MimeType are only the fallback for a photo with no document row; a deleted/deactivated document drops it
              AND (d.Id IS NULL OR (d.IsActive = 1 AND d.IsDeleted = 0))
            ORDER BY i.LawAndRegulationId, i.DisplaySequence, i.Id;

            SELECT [code] AS Code, [description] AS Description
            FROM parameter.Parameters
            WHERE [group] = 'LawRegulationHeader' AND [language] = 'TH' AND [isactive] = 1;
            """;

        using var multi = await connection.QueryMultipleAsync(sql, new { AppraisalId = appraisalId });
        var records = (await multi.ReadAsync<RecordRow>()).ToList();
        if (records.Count == 0)
            return [];

        var images = (await multi.ReadAsync<ImageRow>())
            .Where(r => r.MimeType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true)
            .ToLookup(r => r.LawAndRegulationId);
        var labels = (await multi.ReadAsync<LabelRow>())
            .GroupBy(l => l.Code).ToDictionary(g => g.Key, g => g.First().Description);

        return records
            .OrderBy(r => r.HeaderCode, StringComparer.Ordinal).ThenBy(r => r.CreatedAt).ThenBy(r => r.Id)
            .Select(r => new LawRecord
            {
                Topic = labels.TryGetValue(r.HeaderCode, out var label) && !string.IsNullOrWhiteSpace(label) ? label : r.HeaderCode,
                Remark = string.IsNullOrWhiteSpace(r.Remark) ? null : r.Remark,
                Images = images[r.Id]
                    .Select(i => (Src: AppendixSectionLoader.ToFileUri(i.FilePath), i.Title, i.Description))
                    .Where(i => i.Src is not null)
                    .Select(i => new AppendixImage
                    {
                        ImgSrc = i.Src,
                        Caption = !string.IsNullOrWhiteSpace(i.Title) ? i.Title : string.IsNullOrWhiteSpace(i.Description) ? null : i.Description,
                    }).ToList(),
            })
            .ToList();
    }

    private sealed class RecordRow
    {
        public Guid Id { get; init; }
        public string HeaderCode { get; init; } = string.Empty;
        public string? Remark { get; init; }
        public DateTime? CreatedAt { get; init; }
    }

    private sealed class ImageRow
    {
        public Guid LawAndRegulationId { get; init; }
        public string? Title { get; init; }
        public string? Description { get; init; }
        public string? FilePath { get; init; }
        public string? MimeType { get; init; }
    }

    private sealed class LabelRow
    {
        public string Code { get; init; } = string.Empty;
        public string? Description { get; init; }
    }
}
