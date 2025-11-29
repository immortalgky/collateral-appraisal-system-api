namespace Request.RequestTitles.Features.SyncDraftRequestTitles;

public record SyncDraftRequestTitleCommand(
    Guid SessionId, 
    Guid RequestId, 
    List<RequestTitleDto> requestTitleDtos
) : ICommand<SyncDraftRequestTitleResult>;