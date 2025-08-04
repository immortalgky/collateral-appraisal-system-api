namespace Document.Documents.Features.GetDocumentById;

public record GetDocumentByIdQuery(long Id) : IQuery<GetDocumentByIdResult>;