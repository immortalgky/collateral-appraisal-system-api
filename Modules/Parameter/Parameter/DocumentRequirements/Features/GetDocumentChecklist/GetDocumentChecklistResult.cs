namespace Parameter.DocumentRequirements.Features.GetDocumentChecklist;

public record GetDocumentChecklistResult(
    IReadOnlyList<DocumentChecklistItemDto> ApplicationDocuments,
    IReadOnlyList<PropertyTypeDocumentGroupDto> PropertyTypeGroups);

public record PropertyTypeDocumentGroupDto(
    string PropertyTypeCode,
    string PropertyTypeName,
    IReadOnlyList<DocumentChecklistItemDto> Documents);

public record DocumentChecklistItemDto
{
    public Guid DocumentTypeId { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? Category { get; init; }
    public bool IsRequired { get; init; }
    public string? Notes { get; init; }
}
