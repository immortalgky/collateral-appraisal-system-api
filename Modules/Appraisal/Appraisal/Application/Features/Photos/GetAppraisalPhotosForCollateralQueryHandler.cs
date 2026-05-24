using Appraisal.Contracts.Photos;
using Dapper;

namespace Appraisal.Application.Features.Photos;

/// <summary>
/// Handles cross-module query from Collateral module to resolve a collateral master's
/// photo gallery through the latest engagement's appraisal.
///
/// Design decisions:
/// - Uses ISqlConnectionFactory + Dapper (read-side path, same pattern as
///   GetAppraisalReferenceQueryHandler and GetAppraisalByIdQueryHandler).
/// - Joins appraisal.AppraisalGallery → appraisal.PropertyPhotoMappings so only photos
///   that are actually mapped to a property are returned. This is intentional: the
///   collateral view should show contextually-linked photos, not all uploaded files.
/// - Filters IsInUse = 1 on the gallery row so only confirmed/accepted photos appear.
/// - URL: FilePath is already denormalized onto AppraisalGallery at upload time (from the
///   Document module). We surface it directly as Url. No live Document module call is made
///   during this query — doing so would require N+1 HTTP calls per photo and break the
///   read-side Dapper pattern. If FilePath was not captured at upload time, Url is null
///   and the FE should construct the URL from DocumentId via /documents/{id}/download.
/// - PropertyIds filter: when non-empty, limits results to photos mapped to those
///   AppraisalProperty IDs (i.e., properties belonging to this specific collateral
///   master in the engagement snapshot). When empty, returns all IsInUse photos for
///   the appraisal.
/// </summary>
public class GetAppraisalPhotosForCollateralQueryHandler(
    ISqlConnectionFactory connectionFactory
) : IRequestHandler<GetAppraisalPhotosForCollateralQuery, IReadOnlyList<CollateralPhotoDto>>
{
    public async Task<IReadOnlyList<CollateralPhotoDto>> Handle(
        GetAppraisalPhotosForCollateralQuery query,
        CancellationToken cancellationToken)
    {
        var connection = connectionFactory.GetOpenConnection();
        var parameters = new DynamicParameters();
        parameters.Add("AppraisalId", query.AppraisalId);

        string propertyFilter;
        if (query.PropertyIds is { Count: > 0 })
        {
            parameters.Add("PropertyIds", query.PropertyIds.ToList());
            propertyFilter = "AND m.AppraisalPropertyId IN @PropertyIds";
        }
        else
        {
            propertyFilter = string.Empty;
        }

        var sql = $"""
            SELECT
                g.DocumentId,
                g.PhotoType,
                g.PhotoCategory,
                g.Caption,
                g.FilePath   AS Url,
                m.SequenceNumber AS Sequence,
                m.AppraisalPropertyId AS PropertyId
            FROM appraisal.AppraisalGallery g
            INNER JOIN appraisal.PropertyPhotoMappings m ON m.GalleryPhotoId = g.Id
            WHERE g.AppraisalId = @AppraisalId
              AND g.IsInUse = 1
              {propertyFilter}
            ORDER BY m.SequenceNumber ASC, g.PhotoNumber ASC
            """;

        var rows = await connection.QueryAsync<PhotoRow>(sql, parameters);

        return rows.Select(r => new CollateralPhotoDto(
            r.DocumentId,
            r.PhotoType,
            r.PhotoCategory,
            r.Caption,
            r.Url,
            r.Sequence,
            r.PropertyId
        )).ToList();
    }

    // Dapper projection
    private class PhotoRow
    {
        public Guid DocumentId { get; init; }
        public string PhotoType { get; init; } = null!;
        public string? PhotoCategory { get; init; }
        public string? Caption { get; init; }
        public string? Url { get; init; }
        public int Sequence { get; init; }
        public Guid PropertyId { get; init; }
    }
}
