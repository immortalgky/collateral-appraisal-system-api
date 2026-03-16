namespace Parameter.DocumentRequirements.Features.UpdateDocumentType;

public record UpdateDocumentTypeCommand(
    Guid Id,
    string Name,
    string? Description,
    string? Category,
    int SortOrder,
    bool IsActive) : ICommand<UpdateDocumentTypeResult>;
