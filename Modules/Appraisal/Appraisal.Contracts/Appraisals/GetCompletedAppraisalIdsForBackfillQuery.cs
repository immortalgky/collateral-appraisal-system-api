using Shared.CQRS;

namespace Appraisal.Contracts.Appraisals;

/// <summary>
/// Returns a page of completed appraisal Ids, ordered oldest CompletedAt first.
/// Used by CollateralBackfillJob (Collateral module) to stream historical appraisals in batches.
/// </summary>
public record GetCompletedAppraisalIdsForBackfillQuery(int Page, int PageSize)
    : IQuery<IReadOnlyList<Guid>>;
