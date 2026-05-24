namespace Collateral.CollateralMasters.Events;

/// <summary>
/// Raised when a CollateralMaster's discriminator code changes via LATEST-wins (e.g. L → LB
/// when a building appraisal is applied to an existing bare-land master). Audit-trail parity
/// with CollateralMasterEditedEvent et al.
/// </summary>
public record CollateralTypeChangedEvent(
    Guid MasterId,
    string OldCollateralType,
    string NewCollateralType
) : IDomainEvent;
