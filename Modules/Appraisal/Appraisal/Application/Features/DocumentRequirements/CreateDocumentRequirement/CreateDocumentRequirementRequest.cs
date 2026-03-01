namespace Appraisal.Application.Features.DocumentRequirements.CreateDocumentRequirement;

/// <summary>
/// API request to create a document requirement
/// </summary>
public record CreateDocumentRequirementRequest(
    Guid DocumentTypeId,
    string? PropertyTypeCode,
    string? PurposeCode,
    bool IsRequired,
    string? Notes);
