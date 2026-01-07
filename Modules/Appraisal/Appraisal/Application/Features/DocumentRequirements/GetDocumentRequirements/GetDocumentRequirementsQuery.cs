namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentRequirements;

/// <summary>
/// Query to get all document requirements (admin view)
/// </summary>
public record GetDocumentRequirementsQuery(string? CollateralTypeCode = null) : IQuery<GetDocumentRequirementsResult>;
