namespace Parameter.Contracts.DocumentRequirements;

public record DocumentChecklistItemDto
{
    public string Code { get; init; } = null!;
    public string Name { get; init; } = null!;
    public string? Category { get; init; }
    public bool IsRequired { get; init; }
    public string? Notes { get; init; }
}

public record CollateralTypeDocumentGroupDto(
    string CollateralTypeCode,
    IReadOnlyList<DocumentChecklistItemDto> Documents);
