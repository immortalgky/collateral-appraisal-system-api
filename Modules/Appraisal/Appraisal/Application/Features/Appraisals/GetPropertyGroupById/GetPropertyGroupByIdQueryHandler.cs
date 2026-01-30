namespace Appraisal.Application.Features.Appraisals.GetPropertyGroupById;

/// <summary>
/// Handler for getting a property group by ID
/// </summary>
public class GetPropertyGroupByIdQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetPropertyGroupByIdQuery, GetPropertyGroupByIdResult>
{
    public async Task<GetPropertyGroupByIdResult> Handle(
        GetPropertyGroupByIdQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(query.AppraisalId, cancellationToken)
                        ?? throw new InvalidOperationException($"Appraisal {query.AppraisalId} not found");

        var group = appraisal.Groups.FirstOrDefault(g => g.Id == query.GroupId)
                    ?? throw new InvalidOperationException($"Property group {query.GroupId} not found");

        var propertyItems = group.Items.Select(i => new PropertyGroupItemDto(
            i.AppraisalPropertyId,
            i.SequenceInGroup
        )).ToList();

        return new GetPropertyGroupByIdResult(
            group.Id,
            group.GroupNumber,
            group.GroupName,
            group.Description,
            group.UseSystemCalc,
            propertyItems
        );
    }
}

public record PropertyGroupItemDto(
    Guid PropertyId,
    int SequenceInGroup
);
