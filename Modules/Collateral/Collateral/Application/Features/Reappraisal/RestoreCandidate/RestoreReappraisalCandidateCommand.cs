namespace Collateral.Application.Features.Reappraisal.RestoreCandidate;

/// <summary>
/// Puts a book staff had marked "not reviewing this round" (Status = Deleted) back on the to-do list.
/// Only a Deleted book can be restored; anything else reports not found.
/// </summary>
public record RestoreReappraisalCandidateCommand(Guid Id)
    : ICommand<RestoreReappraisalCandidateResult>;

public record RestoreReappraisalCandidateResult(bool Success);
