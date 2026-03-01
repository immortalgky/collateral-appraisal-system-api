namespace Appraisal.Application.Features.DocumentRequirements.GetDocumentRequirements;

/// <summary>
/// Query to get all document requirements (admin view)
/// </summary>
public record GetDocumentRequirementsQuery(
    string? PropertyTypeCode = null,
    string? PurposeCode = null) : IQuery<GetDocumentRequirementsResult>;
