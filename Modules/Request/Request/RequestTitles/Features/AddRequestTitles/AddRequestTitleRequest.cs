namespace Request.RequestTitles.Features.AddRequestTitles;

public record AddRequestTitlesRequest(
    List<RequestTitleDto> RequestTitles
);