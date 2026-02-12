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

                if (item is not null)
                    result.Properties.Add(item);

                return result;
            },
            new
            {
                query.AppraisalId,
                PropertyGroupId = query.GroupId
            },
            splitOn: "PropertyGroupItemId"
        );

        if (result is null)
            throw new InvalidOperationException($"Property group {query.GroupId} not found");

        return lookup.First().Value;
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
    public Guid PropertyId { get; set; }
    public int SequenceInGroup { get; set; }
    public string PropertyType { get; set; } = default!;
    public Guid AppraisalDetailId { get; set; }
    public string PropertyName { get; set; } = default!;
    public decimal? Area { get; set; }
    public string? Location { get; set; }
}