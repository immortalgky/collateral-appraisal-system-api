namespace Request.RequestTitles.Features.DraftRequestTitle;

public record DraftRequestTitleCommand(
    Guid RequestId,
    List<RequestTitlesCommandDto> AddRequestTitleCommandDtos
) : ICommand<DraftRequestTitleResult>;