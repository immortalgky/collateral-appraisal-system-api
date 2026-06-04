namespace Collateral.Application.Features.BlockReappraisal.MarkBlockReappraisalNotRequired;

/// <summary>
/// Marks a block-project CollateralMaster as excluded from reappraisal and
/// consumes the corresponding BlockReappraisalDue row so it leaves the Pending list.
/// </summary>
public record MarkBlockReappraisalNotRequiredCommand(Guid CollateralMasterId)
    : ICommand<MarkBlockReappraisalNotRequiredResult>;

public record MarkBlockReappraisalNotRequiredResult(Guid CollateralMasterId);
