namespace Collateral.CollateralMasters.Events;

public record CollateralEngagementAddedEvent(
    Guid MasterId,
    Guid EngagementId,
    Guid AppraisalId
) : IDomainEvent;
