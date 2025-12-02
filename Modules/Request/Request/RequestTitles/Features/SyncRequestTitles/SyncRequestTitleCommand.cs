namespace Request.RequestTitles.Features.SyncRequestTitles;

public record SyncRequestTitlesCommand : ICommand<SyncRequestTitlesResult>
{
    public Guid SessionId { get; init; }
    public Guid RequestId { get; init; }
    public List<RequestTitleDto> RequestTitleDtos { get; init; } = new List<RequestTitleDto>();
};