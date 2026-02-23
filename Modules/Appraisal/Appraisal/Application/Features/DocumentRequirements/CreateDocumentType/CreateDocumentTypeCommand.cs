namespace Appraisal.Application.Features.DocumentRequirements.CreateDocumentType;

/// <summary>
/// Command to create a new document type
/// </summary>
public record CreateDocumentTypeCommand(
    string Code,
    string Name,
    string? Description,
    string? Category,
    int SortOrder = 0) : ICommand<CreateDocumentTypeResult>;
