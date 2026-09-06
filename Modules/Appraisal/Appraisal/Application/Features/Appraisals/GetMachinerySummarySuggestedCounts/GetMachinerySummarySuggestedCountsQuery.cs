namespace Appraisal.Application.Features.Appraisals.GetMachinerySummarySuggestedCounts;

/// <summary>
/// Query to derive the machinery summary head-counts from the machines actually recorded
/// on the appraisal, so the appraiser is offered a starting value instead of a blank field.
/// </summary>
public record GetMachinerySummarySuggestedCountsQuery(
    Guid AppraisalId
) : IQuery<GetMachinerySummarySuggestedCountsResult>;
