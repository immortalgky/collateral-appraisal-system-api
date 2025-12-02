namespace Request.RequestTitles.Features.GetRequestTitleById;

public record GetRequestTitleByIdQuery(Guid RequestId, Guid Id) : IQuery<GetRequestTitleByIdResult>;
