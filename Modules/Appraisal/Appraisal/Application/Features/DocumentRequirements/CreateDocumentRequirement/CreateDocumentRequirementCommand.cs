namespace Appraisal.Application.Features.DocumentRequirements.CreateDocumentRequirement;

/// <summary>
/// Command to create a new document requirement
/// </summary>
public record CreateDocumentRequirementCommand(
    Guid DocumentTypeId,
    string? CollateralTypeCode,
    bool IsRequired,
    string? Notes) : ICommand<CreateDocumentRequirementResult>;
