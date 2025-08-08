namespace Request.RequestTitles.Features.GetRequestTitlesByRequestId;

public record GetRequestTitlesByRequestIdQuery(long RequestId) : IQuery<GetRequestTitlesByRequestIdResult>;