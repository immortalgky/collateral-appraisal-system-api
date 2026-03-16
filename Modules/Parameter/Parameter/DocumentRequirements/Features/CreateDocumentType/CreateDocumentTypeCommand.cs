namespace Parameter.DocumentRequirements.Features.CreateDocumentType;

public record CreateDocumentTypeCommand(
    string Code,
    string Name,
    string? Description,
    string? Category,
    int SortOrder = 0) : ICommand<CreateDocumentTypeResult>;
