using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetMachinerySummary;

/// <summary>
/// Query to get the machinery appraisal summary for an appraisal
/// </summary>
public record GetMachinerySummaryQuery(
    Guid AppraisalId
) : IQuery<GetMachinerySummaryResult>;
