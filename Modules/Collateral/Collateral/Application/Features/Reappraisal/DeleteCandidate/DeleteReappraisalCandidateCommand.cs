using Collateral.CollateralMasters.Reappraisal;

namespace Collateral.Application.Features.Reappraisal.DeleteCandidate;

/// <summary>
/// Marks a book "not reviewing this round" (Status = Deleted). It moves to the list's Deleted tab
/// (GetReappraisalCandidatesQuery.Status) until restored. A Consumed book cannot be marked.
/// </summary>
public record DeleteReappraisalCandidateCommand(Guid Id)
    : ICommand<DeleteReappraisalCandidateResult>;
