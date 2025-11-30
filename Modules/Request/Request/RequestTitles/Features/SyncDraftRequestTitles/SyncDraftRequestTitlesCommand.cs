namespace Request.RequestTitles.Features.SyncDraftRequestTitles;

public record SyncDraftRequestTitlesCommand(
    Guid SessionId, 
    Guid RequestId, 
    List<RequestTitleDto> requestTitleDtos
) : ICommand<SyncDraftRequestTitlesResult>;