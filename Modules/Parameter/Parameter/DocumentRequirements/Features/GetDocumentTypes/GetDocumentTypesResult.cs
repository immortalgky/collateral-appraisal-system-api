namespace Parameter.DocumentRequirements.Features.GetDocumentTypes;

public record GetDocumentTypesResult(IReadOnlyList<DocumentTypeDto> DocumentTypes);

public record DocumentTypeDto
{
    public Guid Id { get; init; }
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? Description { get; init; }
    public string? Category { get; init; }
    public bool IsActive { get; init; }
    public int SortOrder { get; init; }
    public DateTime? CreatedOn { get; init; }
    public DateTime? UpdatedOn { get; init; }
}
