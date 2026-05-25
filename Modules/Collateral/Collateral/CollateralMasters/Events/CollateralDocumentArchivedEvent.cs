namespace Collateral.CollateralMasters.Events;

public record CollateralDocumentArchivedEvent(
    Guid MasterId,
    Guid DocumentRowId,
    Guid DocumentId
) : IDomainEvent;
