using Dapper;

namespace Appraisal.Application.Features.Appraisals.GetPropertyGroupById;

/// <summary>
/// Handler for getting a property group by ID
/// </summary>
public class GetPropertyGroupByIdQueryHandler(
    ISqlConnectionFactory sqlConnectionFactory
) : IQueryHandler<GetPropertyGroupByIdQuery, GetPropertyGroupByIdResult>
{
    public async Task<GetPropertyGroupByIdResult> Handle(
        GetPropertyGroupByIdQuery query,
        CancellationToken cancellationToken)
    {
        var sql = """
                    SELECT * 
                    FROM appraisal.vw_PropertyGroupDetail 
                    WHERE AppraisalId = @AppraisalId AND PropertyGroupId = @PropertyGroupId
                  """;

        var connection = sqlConnectionFactory.GetOpenConnection();

        var lookup = new Dictionary<Guid, GetPropertyGroupByIdResult>();

        var result = await connection.QueryAsync<PropertyGroupDto, PropertyGroupItemDto, GetPropertyGroupByIdResult>(
            sql,
            (group, item) =>
            {
                if (!lookup.TryGetValue(group.PropertyGroupId, out var result))
                {
                    result = new GetPropertyGroupByIdResult(
                        group.PropertyGroupId,
                        group.GroupNumber ?? 0,
                        group.GroupName ?? string.Empty,
                        group.Description,
                        group.PricingAnalysisId,
                        new List<PropertyGroupItemDto>()
                    );
                    lookup.Add(group.PropertyGroupId, result);
                }

                if (item is not null && item.PropertyId is not null)
                    result.Properties?.Add(item);

                return result;
            },
            new
            {
                query.AppraisalId,
                PropertyGroupId = query.GroupId
            },
            splitOn: "PropertyGroupItemId"
        );

        if (result is null || !lookup.Any())
            throw new InvalidOperationException($"Property group {query.GroupId} not found");

        var propertyGroup = lookup.First().Value;

        // Secondary query: fetch photo DocumentIds per property
        var propertyIds = propertyGroup.Properties?
            .Where(p => p.PropertyId is not null)
            .Select(p => p.PropertyId!.Value)
            .ToList();

        if (propertyIds is { Count: > 0 })
        {
            var photoSql = """
                           SELECT PPM.Id AS MappingId, PPM.AppraisalPropertyId, AG.DocumentId, PPM.IsThumbnail
                           FROM appraisal.PropertyPhotoMappings PPM
                           INNER JOIN appraisal.AppraisalGallery AG ON AG.Id = PPM.GalleryPhotoId
                           WHERE PPM.AppraisalPropertyId IN @PropertyIds
                           """;

            var photos = await connection.QueryAsync<PropertyPhotoRow>(
                photoSql,
                new { PropertyIds = propertyIds });

            var photosByProperty = photos
                .GroupBy(p => p.AppraisalPropertyId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var property in propertyGroup.Properties!)
            {
                if (property.PropertyId is not null &&
                    photosByProperty.TryGetValue(property.PropertyId.Value, out var propertyPhotos))
                {
                    property.Photos = propertyPhotos
                        .Select(p => new PropertyPhotoDto(p.MappingId, p.DocumentId, p.IsThumbnail))
                        .ToList();
                }
            }

            // Secondary query: fetch the full land titles per land property.
            // Only land/land-and-building properties have rows (joined via LandAppraisalDetails).
            var titleSql = """
                           SELECT lad.AppraisalPropertyId,
                                  lt.Id,
                                  lt.TitleNumber,
                                  lt.TitleType,
                                  lt.BookNumber,
                                  lt.PageNumber,
                                  lt.LandParcelNumber,
                                  lt.SurveyNumber,
                                  lt.MapSheetNumber,
                                  lt.Rawang,
                                  lt.AerialMapName,
                                  lt.AerialMapNumber,
                                  lt.AreaRai      AS Rai,
                                  lt.AreaNgan     AS Ngan,
                                  lt.AreaSquareWa AS SquareWa,
                                  lt.BoundaryMarkerType,
                                  lt.DocumentValidationResultType,
                                  lt.GovernmentPricePerSqWa,
                                  lt.GovernmentPrice,
                                  lt.Remark
                           FROM appraisal.LandTitles lt
                           INNER JOIN appraisal.LandAppraisalDetails lad ON lad.Id = lt.LandAppraisalDetailId
                           WHERE lad.AppraisalPropertyId IN @PropertyIds
                           """;

            var titles = await connection.QueryAsync<LandTitleDto>(
                titleSql,
                new { PropertyIds = propertyIds });

            var titlesByProperty = titles
                .GroupBy(tt => tt.AppraisalPropertyId)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var property in propertyGroup.Properties!)
            {
                if (property.PropertyId is not null &&
                    titlesByProperty.TryGetValue(property.PropertyId.Value, out var propertyTitles))
                {
                    property.Titles = propertyTitles;
                }
            }
        }

        return propertyGroup;
    }
}

public record PropertyGroupDto
{
    public Guid? AppraisalId { get; set; }
    public Guid PropertyGroupId { get; set; }
    public int? GroupNumber { get; set; }
    public string? GroupName { get; set; }
    public string? Description { get; set; }
    public Guid? PricingAnalysisId { get; set; }
}

public record PropertyGroupItemDto
{
    public Guid? PropertyId { get; set; }
    public int? SequenceInGroup { get; set; }
    public string? PropertyType { get; set; } = default!;
    public Guid? AppraisalDetailId { get; set; }
    public string? PropertyName { get; set; } = default!;
    public decimal? Area { get; set; }
    public decimal? latitude { get; set; }
    public decimal? longitude { get; set; }
    public string? MachineName { get; set; }
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Dimension { get; set; }
    public string? Location { get; set; }
    /// <summary>Title deed no(s): comma-joined LandTitles for land, unit deed for condo.</summary>
    public string? TitleNo { get; set; }
    /// <summary>True for plain land (L/LB) flagged "rented out to others"; null for non-land types.</summary>
    public bool? IsRentedOut { get; set; }
    public List<PropertyPhotoDto>? Photos { get; set; }
    /// <summary>Full land titles (land/land-and-building only); null/empty for other types.</summary>
    public List<LandTitleDto>? Titles { get; set; }
}

public record PropertyPhotoDto(Guid MappingId, Guid DocumentId, bool IsThumbnail);

internal record PropertyPhotoRow(Guid MappingId, Guid AppraisalPropertyId, Guid DocumentId, bool IsThumbnail);

public record LandTitleDto
{
    /// <summary>Used only to group titles onto their property; not meaningful to clients.</summary>
    public Guid AppraisalPropertyId { get; set; }
    public Guid? Id { get; set; }
    public string? TitleNumber { get; set; }
    public string? TitleType { get; set; }
    public string? BookNumber { get; set; }
    public string? PageNumber { get; set; }
    public string? LandParcelNumber { get; set; }
    public string? SurveyNumber { get; set; }
    public string? MapSheetNumber { get; set; }
    public string? Rawang { get; set; }
    public string? AerialMapName { get; set; }
    public string? AerialMapNumber { get; set; }
    public decimal? Rai { get; set; }
    public decimal? Ngan { get; set; }
    public decimal? SquareWa { get; set; }
    public string? BoundaryMarkerType { get; set; }
    public string? DocumentValidationResultType { get; set; }
    public decimal? GovernmentPricePerSqWa { get; set; }
    public decimal? GovernmentPrice { get; set; }
    public string? Remark { get; set; }
}