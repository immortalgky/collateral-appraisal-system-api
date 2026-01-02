using Shared.CQRS;

namespace Document.Domain.Documents.Features.GetDocuments;

public record GetDocumentQuery : IQuery<GetDocumentResult>;