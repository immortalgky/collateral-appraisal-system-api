namespace Collateral.Application.Features.CollateralMasters.Documents.ListDocuments;

/// <summary>
/// Returns all documents attached to a CollateralMaster.
/// Defaults to active documents only (IsActive = true) when IsActive filter is null.
/// </summary>
public record ListCollateralDocumentsQuery(
    Guid CollateralMasterId,
    string? DocumentType,
    bool? IsActive
) : IQuery<ListCollateralDocumentsResult>;

public record ListCollateralDocumentsResult(IReadOnlyList<CollateralDocumentDto> Items);

public record CollateralDocumentDto(
    Guid Id,
    string DocumentType,
    Guid DocumentId,
    string FileName,
    string? Description,
    bool IsActive,
    DateTime? CreatedAt,
    string? CreatedBy
);
