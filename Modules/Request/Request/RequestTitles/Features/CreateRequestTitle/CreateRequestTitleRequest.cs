namespace Request.RequestTitles.Features.CreateRequestTitle;

public record CreateRequestTitleRequest(
    Guid RequestId,
    List<RequestTitleDto> RequestTitleDtos
);