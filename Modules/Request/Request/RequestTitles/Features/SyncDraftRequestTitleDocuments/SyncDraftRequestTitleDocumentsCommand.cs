namespace Request.RequestTitles.Features.SyncDraftRequestTitleDocuments;

public record SyncDraftRequestTitleDocumentsCommand : ICommand<SyncDraftRequestTitleDocumentsResult>
{
    public Guid SessionId { get; init; }
    public Guid RequestId { get; init; }
    public Guid TitleId { get; init; }
    public List<RequestTitleDocumentDto> RequestTitleDocumentDtos { get; init; } = new();
}