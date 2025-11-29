namespace Request.RequestTitles.Features.SyncRequestTitle;

public record SyncRequestTitleCommand(
    Guid SessionId, 
    Guid RequestId, 
    List<RequestTitleDto> requestTitleDtos
) : ICommand<SyncRequestTitleResult>;