namespace Appraisal.Application.Features.Appraisals.GetPropertyGroups;

/// <summary>
/// Handler for getting all property groups
/// </summary>
public class GetPropertyGroupsQueryHandler(
    IAppraisalRepository appraisalRepository
) : IQueryHandler<GetPropertyGroupsQuery, GetPropertyGroupsResult>
{
    public async Task<GetPropertyGroupsResult> Handle(
        GetPropertyGroupsQuery query,
        CancellationToken cancellationToken)
    {
        var appraisal = await appraisalRepository.GetByIdAsync(query.AppraisalId, cancellationToken)
                        ?? throw new InvalidOperationException($"Appraisal {query.AppraisalId} not found");

        var groups = appraisal.Groups.Select(g => new PropertyGroupDto(
            g.Id,
            g.GroupNumber,
            g.GroupName,
            g.Description,
            g.UseSystemCalc,
            g.Items.Count
        )).ToList();

        return new GetPropertyGroupsResult(groups);
    }
}

public record PropertyGroupDto(
    Guid Id,
    int GroupNumber,
    string GroupName,
    string? Description,
    bool UseSystemCalc,
    int PropertyCount
);
