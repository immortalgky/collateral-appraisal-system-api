namespace Request.RequestTitles.Features.GetRequestTitleById;

public record GetRequestTitleByIdQuery(long RequestId, long Id) : IQuery<GetRequestTitleByIdResult>;