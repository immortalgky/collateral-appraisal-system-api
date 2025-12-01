namespace Request.RequestDocuments.Features.GetRequestDocument;

public record GetRequestDocumentQuery(Guid requestId) : IQuery<GetRequestDocumentResult>;