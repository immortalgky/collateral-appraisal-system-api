namespace Appraisal.Application.Features.DocumentRequirements.DeleteDocumentRequirement;

/// <summary>
/// Command to delete (deactivate) a document requirement
/// </summary>
public record DeleteDocumentRequirementCommand(Guid Id) : ICommand<DeleteDocumentRequirementResult>;
