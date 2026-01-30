namespace Appraisal.Application.Features.DocumentRequirements.UpdateDocumentType;

/// <summary>
/// Command to update an existing document type
/// </summary>
public record UpdateDocumentTypeCommand(
    Guid Id,
    string Name,
    string? Description,
    string? Category,
    int SortOrder,
    bool IsActive) : ICommand<UpdateDocumentTypeResult>;
