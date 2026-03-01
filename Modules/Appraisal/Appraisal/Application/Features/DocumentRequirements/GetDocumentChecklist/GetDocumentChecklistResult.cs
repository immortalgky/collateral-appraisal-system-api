namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentChecklist;

/// <summary>
/// Result containing document checklist with application-level and property-type-specific documents
/// </summary>
public record GetDocumentChecklistResult(
    IReadOnlyList<DocumentChecklistItemDto> ApplicationDocuments,
    IReadOnlyList<PropertyTypeDocumentGroupDto> PropertyTypeGroups);

/// <summary>
/// Group of documents for a specific property type
/// </summary>
public record PropertyTypeDocumentGroupDto(
    string PropertyTypeCode,
    string PropertyTypeName,
    IReadOnlyList<DocumentChecklistItemDto> Documents);

/// <summary>
/// Individual document in the checklist
/// </summary>
public record DocumentChecklistItemDto
{
    public Guid DocumentTypeId { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? Category { get; init; }
    public bool IsRequired { get; init; }
    public string? Notes { get; init; }
}
