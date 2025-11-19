namespace Request.RequestTitles.Features.UpdateRequestTitle;

public record UpdateRequestTitleRequest(
    Guid RequestId,
    List<RequestTitleDto> RequestTitleDtos
);