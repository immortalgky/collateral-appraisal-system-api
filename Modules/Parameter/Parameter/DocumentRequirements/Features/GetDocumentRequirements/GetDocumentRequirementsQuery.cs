namespace Parameter.DocumentRequirements.Features.GetDocumentRequirements;

public record GetDocumentRequirementsQuery(
    string? PropertyTypeCode = null,
    string? PurposeCode = null) : IQuery<GetDocumentRequirementsResult>;
