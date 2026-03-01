namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentChecklist;

/// <summary>
/// Query to get document checklist for specified property types and optional purpose
/// </summary>
public record GetDocumentChecklistQuery(
    IEnumerable<string> PropertyTypeCodes,
    string? PurposeCode = null) : IQuery<GetDocumentChecklistResult>;
