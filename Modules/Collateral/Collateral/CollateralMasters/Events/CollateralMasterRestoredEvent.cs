namespace Collateral.CollateralMasters.Events;

public record CollateralMasterRestoredEvent(
    Guid MasterId,
    string Reason,
    string By
) : IDomainEvent;
