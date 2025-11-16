namespace Request.RequestTitles.Features.AddRequestTitles;

public record AddRequestTitlesResult(List<RequestTitleResultDto> Results);

public record RequestTitleResultDto(
    Guid TitleId,
    List<Guid> TitleDocumentIds
    );