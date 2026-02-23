namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentRequirements;

/// <summary>
/// Result containing all document requirements
/// </summary>
public record GetDocumentRequirementsResult(IReadOnlyList<DocumentRequirementDto> Requirements);

/// <summary>
/// DTO for document requirement (admin view)
/// </summary>
public record DocumentRequirementDto
{
    public Guid Id { get; init; }
    public Guid DocumentTypeId { get; init; }
    public string DocumentTypeCode { get; init; } = null!;
    public string DocumentTypeName { get; init; } = null!;
    public string? DocumentTypeCategory { get; init; }
    public string? CollateralTypeCode { get; init; }
    public string? CollateralTypeName { get; init; }
    public bool IsRequired { get; init; }
    public bool IsActive { get; init; }
    public string? Notes { get; init; }
    public DateTime? CreatedOn { get; init; }
    public DateTime? UpdatedOn { get; init; }
}
