using Request.Contracts.RequestDocuments.Dto;

namespace Request.RequestDocuments.Features.UpdateRequestDocument;

public record UpdateRequestDocumentResponse(List<RequestDocumentDto> Documents);