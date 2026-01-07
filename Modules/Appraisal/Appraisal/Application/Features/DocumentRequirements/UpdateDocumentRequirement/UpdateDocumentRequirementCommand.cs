namespace Appraisal.Application.Features.DocumentRequirements.UpdateDocumentRequirement;

/// <summary>
/// Command to update an existing document requirement
/// </summary>
public record UpdateDocumentRequirementCommand(
    Guid Id,
    bool IsRequired,
    bool IsActive,
    string? Notes) : ICommand<UpdateDocumentRequirementResult>;
