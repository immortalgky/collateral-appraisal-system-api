namespace Parameter.DocumentRequirements.Features.DeleteDocumentType;

public record DeleteDocumentTypeCommand(Guid Id) : ICommand<DeleteDocumentTypeResult>;
