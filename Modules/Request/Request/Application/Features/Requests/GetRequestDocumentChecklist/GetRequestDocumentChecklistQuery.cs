namespace Request.Application.Features.Requests.GetRequestDocumentChecklist;

public record GetRequestDocumentChecklistQuery(Guid RequestId) : IQuery<GetRequestDocumentChecklistResult>;
