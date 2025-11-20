namespace Request.RequestTitles.Features.CreateRequestTitle;

public record CreateRequestTitleRequest(
    List<RequestTitleDto> RequestTitleDtos
);