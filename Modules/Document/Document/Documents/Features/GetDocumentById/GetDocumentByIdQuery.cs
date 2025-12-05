using Shared.CQRS;

namespace Document.Documents.Features.GetDocumentById;

public record GetDocumentByIdQuery(Guid Id) : IQuery<GetDocumentByIdResult>;