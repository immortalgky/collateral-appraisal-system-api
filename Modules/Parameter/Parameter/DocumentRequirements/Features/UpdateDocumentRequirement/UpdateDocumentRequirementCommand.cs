namespace Parameter.DocumentRequirements.Features.UpdateDocumentRequirement;

public record UpdateDocumentRequirementCommand(
    Guid Id,
    bool IsRequired,
    bool IsActive,
    string? Notes) : ICommand<UpdateDocumentRequirementResult>;
