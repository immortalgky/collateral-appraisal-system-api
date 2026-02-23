namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentChecklist;

/// <summary>
/// Query to get document checklist for specified collateral types
/// </summary>
public record GetDocumentChecklistQuery(IEnumerable<string> CollateralTypeCodes) : IQuery<GetDocumentChecklistResult>;
