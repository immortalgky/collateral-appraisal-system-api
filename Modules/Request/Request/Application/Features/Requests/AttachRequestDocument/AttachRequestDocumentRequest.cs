namespace Request.Application.Features.Requests.AttachRequestDocument;

public record AttachRequestDocumentRequest(
    Guid DocumentId,
    string DocumentType,
    string? FileName,
    string? Source = "REQUEST");
