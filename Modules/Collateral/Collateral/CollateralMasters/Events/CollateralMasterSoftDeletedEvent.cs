namespace Collateral.CollateralMasters.Events;

public record CollateralMasterSoftDeletedEvent(
    Guid MasterId,
    string Reason,
    string By
) : IDomainEvent;
