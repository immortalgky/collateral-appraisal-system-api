namespace Request.RequestDocuments.Features.RemoveRequestDocument;

public record RemoveRequestDocumentCommand(Guid Id) : ICommand<RemoveRequestDocumentResult>;
