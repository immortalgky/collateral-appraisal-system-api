namespace Request.RequestTitles.Features.SyncDraftRequestTitles;

public record SyncDraftRequestTitlesCommand : ICommand<SyncDraftRequestTitlesResult>
{
    public Guid SessionId { get; init; }
    public Guid RequestId { get; init; }
    public List<RequestTitleDto> RequestTitleDtos { get; init; } = new List<RequestTitleDto>();
};