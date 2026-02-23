namespace Request.Application.Features.RequestDocuments.GetRequestDocumentsByRequestId;

public record GetRequestDocumentsByRequestIdResponse(
    int TotalDocuments,
    int TotalUploaded,
    List<DocumentSectionDto> Sections);
