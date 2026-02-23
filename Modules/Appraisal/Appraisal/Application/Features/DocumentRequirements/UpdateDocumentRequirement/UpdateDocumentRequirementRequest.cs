namespace Appraisal.Application.Features.DocumentRequirements.UpdateDocumentRequirement;

/// <summary>
/// API request to update a document requirement
/// </summary>
public record UpdateDocumentRequirementRequest(
    bool IsRequired,
    bool IsActive,
    string? Notes);
