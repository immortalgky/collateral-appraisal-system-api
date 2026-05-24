namespace Collateral.Application.Features.CollateralMasters.Documents.AttachDocument;

/// <summary>
/// Attaches a previously-uploaded document to a CollateralMaster.
/// The caller must upload the file to the Document module first
/// (POST /api/v1/documents) and pass in the returned DocumentId.
/// </summary>
public record AttachCollateralDocumentCommand(
    Guid CollateralMasterId,
    string DocumentType,
    Guid DocumentId,
    string FileName,
    string? Description
) : ICommand<AttachCollateralDocumentResult>;

public record AttachCollateralDocumentResult(Guid DocumentRowId);
