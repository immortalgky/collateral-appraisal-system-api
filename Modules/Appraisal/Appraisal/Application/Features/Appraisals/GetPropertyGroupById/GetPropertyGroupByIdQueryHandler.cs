using Dapper;

namespace Appraisal.Application.Features.Appraisals.GetPropertyGroupById;

/// <summary>
/// Handler for getting a property group by ID
/// </summary>
public class GetPropertyGroupByIdQueryHandler(
    IAppraisalRepository appraisalRepository,
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
                        true,
                        new List<PropertyGroupItemDto>()
                    );
                    lookup.Add(group.PropertyGroupId, result);
                }

                if (item.PropertyId is not null)
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
                           SELECT PPM.AppraisalPropertyId, AG.DocumentId, PPM.IsThumbnail
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
                        .Select(p => new PropertyPhotoDto(p.DocumentId, p.IsThumbnail))
                        .ToList();
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
}

public record PropertyGroupItemDto
{
    public Guid? PropertyId { get; set; }
    public int? SequenceInGroup { get; set; }
    public string? PropertyType { get; set; } = default!;
    public Guid? AppraisalDetailId { get; set; }
    public string? PropertyName { get; set; } = default!;
    public decimal? Area { get; set; }
    public string? Location { get; set; }
    public List<PropertyPhotoDto>? Photos { get; set; }
}

public record PropertyPhotoDto(Guid DocumentId, bool IsThumbnail);

internal record PropertyPhotoRow(Guid AppraisalPropertyId, Guid DocumentId, bool IsThumbnail);