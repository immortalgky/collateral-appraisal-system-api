using Shared.CQRS;

namespace Appraisal.Application.Features.Appraisals.GetCompletedAppraisalIdsForBackfill;

/// <summary>
/// Returns a page of completed appraisal Ids, ordered oldest CompletedAt first.
/// Used by CollateralBackfillJob to stream historical appraisals in batches.
/// </summary>
public record GetCompletedAppraisalIdsForBackfillQuery(int Page, int PageSize)
    : IQuery<IReadOnlyList<Guid>>;
