namespace Request.RequestDocuments.Features.AddRequestDocument;

using Request.Contracts.RequestDocuments.Dto;

public record AddRequestDocumentRequest(
    List<RequestDocumentDto> Documents
);
