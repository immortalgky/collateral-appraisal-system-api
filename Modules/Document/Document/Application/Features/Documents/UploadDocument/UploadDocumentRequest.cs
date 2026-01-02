namespace Document.Domain.Documents.Features.UploadDocument;
public record UploadDocumentRequest(
    List<UploadResultDto> Result
    );