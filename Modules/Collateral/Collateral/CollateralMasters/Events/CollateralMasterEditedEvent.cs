namespace Collateral.CollateralMasters.Events;

public record CollateralMasterEditedEvent(
    Guid MasterId,
    string ChangedFields,
    string Reason,
    string By
) : IDomainEvent;
