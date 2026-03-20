namespace Parameter.DocumentRequirements.Features.GetDocumentChecklist;

public record GetDocumentChecklistQuery(
    IEnumerable<string> PropertyTypeCodes,
    string? PurposeCode = null) : IQuery<GetDocumentChecklistResult>;
