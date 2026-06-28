namespace Parameter.DocumentRequirements.Features.GetDocumentRequirements;

public record GetDocumentRequirementsQuery(
    string? PropertyTypeCode = null,
    string? PurposeCode = null,
    bool IncludeInactive = false) : IQuery<GetDocumentRequirementsResult>;
