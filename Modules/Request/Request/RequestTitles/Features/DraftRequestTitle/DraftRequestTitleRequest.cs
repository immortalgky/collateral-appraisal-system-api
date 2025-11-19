namespace Request.RequestTitles.Features.DraftRequestTitle;

public record DraftRequestTitleRequest(
    Guid RequestId,
    List<RequestTitleDto> RequestTitleDtos
);
