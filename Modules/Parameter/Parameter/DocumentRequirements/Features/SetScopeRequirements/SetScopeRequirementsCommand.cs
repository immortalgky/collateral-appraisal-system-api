namespace Parameter.DocumentRequirements.Features.SetScopeRequirements;

public record ScopeRequirementItem(Guid DocumentTypeId, bool IsRequired);

public record SetScopeRequirementsCommand(
    string? PropertyTypeCode,
    string? PurposeCode,
    IReadOnlyList<ScopeRequirementItem> Items) : ICommand<SetScopeRequirementsResult>;

public record SetScopeRequirementsResult(bool Success);
