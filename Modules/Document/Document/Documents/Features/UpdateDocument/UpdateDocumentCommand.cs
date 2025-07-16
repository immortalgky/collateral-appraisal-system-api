namespace Document.Documents.Features.UpdateDocument;

public record UpdateDocumentCommand(long Id, string NewComment) : ICommand<UpdateDocumentResult>;