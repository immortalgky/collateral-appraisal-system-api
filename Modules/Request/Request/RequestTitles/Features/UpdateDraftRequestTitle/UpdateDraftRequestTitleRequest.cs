namespace Request.RequestTitles.Features.UpdateDraftRequestTitle;

public record UpdateDraftRequestTitleRequest(
    Guid requestId,
    List<RequestTitleDto> RequestTitleDtos
);