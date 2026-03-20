namespace Parameter.DocumentRequirements.Features.CreateDocumentRequirement;

public record CreateDocumentRequirementCommand(
    Guid DocumentTypeId,
    string? PropertyTypeCode,
    string? PurposeCode,
    bool IsRequired,
    string? Notes) : ICommand<CreateDocumentRequirementResult>;
