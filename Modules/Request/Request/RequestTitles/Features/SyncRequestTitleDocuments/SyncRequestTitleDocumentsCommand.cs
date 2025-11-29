namespace Request.RequestTitles.Features.SyncRequestTitleDocuments;

public record SyncRequestTitleDocumentsCommand : ICommand<SyncRequestTitleDocumentsResult>
{
    public Guid SessionId { get; init; }
    public Guid RequestId { get; init; }
    public Guid TitleId { get; init; }
    public List<RequestTitleDocumentDto> RequestTitleDocumentDtos { get; init; } = new();
}