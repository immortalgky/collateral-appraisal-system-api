namespace Collateral.Application.Features.CollateralMasters.Documents.ArchiveDocument;

/// <summary>
/// Soft-archives a CollateralDocument row, setting IsActive = false.
/// The DocumentRowId is the PK of the CollateralDocument row, NOT the upstream Document module's DocumentId.
/// </summary>
public record ArchiveCollateralDocumentCommand(
    Guid CollateralMasterId,
    Guid DocumentRowId
) : ICommand;
