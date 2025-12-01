namespace Request.RequestTitles.Features.GetRequestTitleById;

public record GetRequestTitleByIdQuery(Guid RequestId, long Id) : IQuery<GetRequestTitleByIdResult>;