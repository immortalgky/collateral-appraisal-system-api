using Shared.CQRS;

namespace Document.Documents.Features.GetDocuments;

public record GetDocumentQuery : IQuery<GetDocumentResult>;