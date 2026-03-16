namespace Parameter.DocumentRequirements.Features.CreateDocumentRequirement;

public record CreateDocumentRequirementRequest(
    Guid DocumentTypeId,
    string? PropertyTypeCode,
    string? PurposeCode,
    bool IsRequired,
    string? Notes);
