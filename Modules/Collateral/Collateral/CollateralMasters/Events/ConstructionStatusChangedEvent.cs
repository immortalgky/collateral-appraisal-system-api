namespace Collateral.CollateralMasters.Events;

public record ConstructionStatusChangedEvent(
    Guid MasterId,
    bool WasUnderConstruction,
    bool IsUnderConstruction,
    decimal? FromPercent,
    decimal? ToPercent
) : IDomainEvent;
