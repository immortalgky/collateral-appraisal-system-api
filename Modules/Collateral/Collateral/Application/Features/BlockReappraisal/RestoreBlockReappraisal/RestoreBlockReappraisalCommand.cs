namespace Collateral.Application.Features.BlockReappraisal.RestoreBlockReappraisal;

/// <summary>
/// Puts a Deleted (not required) BlockReappraisalDue row back to Pending, so the project returns to the due list.
/// </summary>
public record RestoreBlockReappraisalCommand(Guid CollateralMasterId)
    : ICommand<RestoreBlockReappraisalResult>;

public record RestoreBlockReappraisalResult(Guid CollateralMasterId);
