namespace Collateral.CollateralMasters.Events;

public record CollateralMasterCreatedEvent(
    Guid Id,
    string CollateralType
) : IDomainEvent;
