namespace Request.RequestTitles.Features.SyncDraftRequestTitleDocuments;

public record SyncDraftRequestTitleDocumentsResult(
    List<RequestTitleDocumentDto> RequestTitleDocumentDtos
    );