using Request.Contracts.RequestDocuments.Dto;

namespace Request.RequestDocuments.Features.GetRequestDocument;

public record GetRequestDocumentResult(List<RequestDocument> Documents);
