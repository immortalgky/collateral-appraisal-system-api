namespace Parameter.DocumentRequirements.Features.DeleteDocumentRequirement;

public record DeleteDocumentRequirementCommand(Guid Id) : ICommand<DeleteDocumentRequirementResult>;
