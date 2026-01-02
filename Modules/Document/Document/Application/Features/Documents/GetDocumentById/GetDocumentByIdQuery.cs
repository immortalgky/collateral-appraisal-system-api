using Shared.CQRS;

namespace Document.Domain.Documents.Features.GetDocumentById;

public record GetDocumentByIdQuery(Guid Id) : IQuery<GetDocumentByIdResult>;