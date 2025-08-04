namespace Document.Documents.Features.DeleteDocument;

public record DeleteDocumentCommand(long Id) : ICommand<DeleteDocumentResult>;