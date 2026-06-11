using Collateral.CollateralMasters.Reappraisal;

namespace Collateral.Application.Features.Reappraisal.DeleteCandidate;

/// <summary>
/// Soft-deletes a reappraisal candidate (Status = Deleted).
/// Deleted candidates no longer appear in the list (filtered in vw_ReappraisalCandidates).
/// </summary>
public record DeleteReappraisalCandidateCommand(Guid Id)
    : ICommand<DeleteReappraisalCandidateResult>;
