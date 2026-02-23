namespace Appraisal.Application.Features.DocumentRequirements.DeleteDocumentType;

/// <summary>
/// Command to delete (deactivate) a document type
/// </summary>
public record DeleteDocumentTypeCommand(Guid Id) : ICommand<DeleteDocumentTypeResult>;
