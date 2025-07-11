namespace Document.Documents.Features.UploadDocument;
public record UploadDocumentCommand(
    List<IFormFile> Documents,
    string RelateRequest,
    long RelateId
) : ICommand<UploadDocumentResult>;