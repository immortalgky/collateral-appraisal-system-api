namespace Request.RequestTitles.Features.GetRequestTitlesByRequestId;

public record GetRequestTitlesByRequestIdQuery(Guid RequestId) : IQuery<GetRequestTitlesByRequestIdResult>;