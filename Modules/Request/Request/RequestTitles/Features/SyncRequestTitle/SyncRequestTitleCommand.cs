namespace Request.RequestTitles.Features.SyncRequestTitle;

public record SyncRequestTitleCommand : ICommand<SyncRequestTitleResult>
{
    public Guid SessionId { get; init; }
    public Guid RequestId { get; init; }
    public List<RequestTitleDto> RequestTitleDtos { get; init; } = new List<RequestTitleDto>();
};