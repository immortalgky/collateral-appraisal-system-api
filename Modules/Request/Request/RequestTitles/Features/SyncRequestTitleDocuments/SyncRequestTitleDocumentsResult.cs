namespace Request.RequestTitles.Features.SyncRequestTitleDocuments;

public record SyncRequestTitleDocumentsResult(
    List<RequestTitleDocumentDto> RequestTitleDocumentDtos
    );