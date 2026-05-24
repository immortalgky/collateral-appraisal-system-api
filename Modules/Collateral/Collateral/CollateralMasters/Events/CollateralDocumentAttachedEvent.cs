namespace Collateral.CollateralMasters.Events;

public record CollateralDocumentAttachedEvent(
    Guid MasterId,
    Guid DocumentRowId,
    Guid DocumentId,
    string DocumentType,
    string FileName
) : IDomainEvent;
